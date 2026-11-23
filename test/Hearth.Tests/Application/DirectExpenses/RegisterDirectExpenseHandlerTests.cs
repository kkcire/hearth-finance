using Hearth.Application.Authorization;
using Hearth.Application.DirectExpenses.RegisterDirectExpense;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.DirectExpenses;

public class RegisterDirectExpenseHandlerTests
{
    private static readonly DateOnly InitialDate = new(2026, 10, 1);

    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly InMemoryDirectExpenseRepository _directExpenses = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RegisterDirectExpenseHandler _handler;

    public RegisterDirectExpenseHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new RegisterDirectExpenseHandler(_accountAccess, _categories, _directExpenses, _unitOfWork);
    }

    private async Task<(Account Account, Category Category)> SeedAccountAndCategoryAsync()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta principal",
            Money.FromDecimal(1000.00m),
            InitialDate);
        await _accounts.AddAsync(account);

        var category = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Lanches");
        await _categories.AddAsync(category);

        return (account, category);
    }

    [Fact]
    public async Task ExecuteAsync_ValidCommand_CreatesPersistsAndReturnsId()
    {
        var (account, category) = await SeedAccountAndCategoryAsync();

        var id = await _handler.ExecuteAsync(new RegisterDirectExpenseCommand(
            account.Id,
            new DateOnly(2026, 10, 15),
            85.40m,
            "  Mercado  ",
            category.Id));

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);

        var saved = await _directExpenses.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(account.Id, saved.AccountId);
        Assert.Equal(new DateOnly(2026, 10, 15), saved.Date);
        Assert.Equal(Money.FromDecimal(85.40m), saved.Amount);
        Assert.Equal("Mercado", saved.Description);
        Assert.Equal(category.Id, saved.CategoryId);
    }

    [Fact]
    public async Task ExecuteAsync_OnInitialBalanceDate_IsAccepted()
    {
        var (account, category) = await SeedAccountAndCategoryAsync();

        var id = await _handler.ExecuteAsync(new RegisterDirectExpenseCommand(
            account.Id, InitialDate, 20.00m, "Café", category.Id));

        Assert.NotNull(await _directExpenses.GetByIdAsync(id));
    }

    [Fact]
    public async Task ExecuteAsync_BeforeInitialBalanceDate_ThrowsDomainExceptionAndDoesNotSave()
    {
        var (account, category) = await SeedAccountAndCategoryAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new RegisterDirectExpenseCommand(
                account.Id, InitialDate.AddDays(-1), 20.00m, "Café", category.Id)));

        Assert.Empty(_directExpenses.Expenses);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ArchivedCategory_ThrowsDomainExceptionAndDoesNotSave()
    {
        var (account, category) = await SeedAccountAndCategoryAsync();
        category.Archive();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new RegisterDirectExpenseCommand(
                account.Id, InitialDate, 20.00m, "Café", category.Id)));

        Assert.Empty(_directExpenses.Expenses);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_CategoryFromAnotherUser_ThrowsNotFoundAndDoesNotSave()
    {
        var (account, _) = await SeedAccountAndCategoryAsync();
        var otherCategory = Category.CreateForUser(Guid.NewGuid(), Guid.NewGuid(), "Outra");
        await _categories.AddAsync(otherCategory);

        await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new RegisterDirectExpenseCommand(
                account.Id, InitialDate, 20.00m, "Café", otherCategory.Id)));

        Assert.Empty(_directExpenses.Expenses);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFoundAndDoesNotSave()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new RegisterDirectExpenseCommand(
                unknownId, InitialDate, 20.00m, "Café", Guid.NewGuid())));

        Assert.Equal(nameof(Account), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
        Assert.Empty(_directExpenses.Expenses);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownCategory_ThrowsNotFoundAndDoesNotSave()
    {
        var (account, _) = await SeedAccountAndCategoryAsync();
        var unknownCategoryId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new RegisterDirectExpenseCommand(
                account.Id, InitialDate, 20.00m, "Café", unknownCategoryId)));

        Assert.Equal(nameof(Category), exception.ResourceName);
        Assert.Equal(unknownCategoryId, exception.ResourceId);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task ExecuteAsync_NonPositiveAmount_ThrowsAndDoesNotSave(double raw)
    {
        var (account, category) = await SeedAccountAndCategoryAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _handler.ExecuteAsync(
            new RegisterDirectExpenseCommand(
                account.Id, InitialDate, (decimal)raw, "Café", category.Id)));

        Assert.Empty(_directExpenses.Expenses);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_AmountWithMoreThanTwoDecimalPlaces_ThrowsAndDoesNotSave()
    {
        var (account, category) = await SeedAccountAndCategoryAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.ExecuteAsync(
            new RegisterDirectExpenseCommand(
                account.Id, InitialDate, 10.005m, "Café", category.Id)));

        Assert.Empty(_directExpenses.Expenses);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsCancellationToken()
    {
        var (account, category) = await SeedAccountAndCategoryAsync();
        using var cts = new CancellationTokenSource();

        await _handler.ExecuteAsync(
            new RegisterDirectExpenseCommand(account.Id, InitialDate, 15.00m, "Café", category.Id),
            cts.Token);

        Assert.Equal(cts.Token, _accounts.LastCancellationToken);
        Assert.Equal(cts.Token, _categories.LastCancellationToken);
        Assert.Equal(cts.Token, _directExpenses.LastCancellationToken);
        Assert.Equal(cts.Token, _unitOfWork.LastCancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_NullCommand_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.ExecuteAsync(null!));
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RegisterDirectExpenseHandler(null!, _categories, _directExpenses, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new RegisterDirectExpenseHandler(_accountAccess, null!, _directExpenses, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new RegisterDirectExpenseHandler(_accountAccess, _categories, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new RegisterDirectExpenseHandler(_accountAccess, _categories, _directExpenses, null!));
    }
}
