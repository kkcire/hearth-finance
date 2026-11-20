using Hearth.Application.Exceptions;
using Hearth.Application.FixedExpenses.RegisterFixedExpense;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.FixedExpenses;

public class RegisterFixedExpenseHandlerTests
{
    private static readonly DateOnly InitialDate = new(2026, 10, 15);

    private readonly InMemoryAccountRepository _accounts = new();
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly InMemoryFixedExpenseRepository _fixedExpenses = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RegisterFixedExpenseHandler _handler;

    public RegisterFixedExpenseHandlerTests()
    {
        _handler = new RegisterFixedExpenseHandler(_accounts, _categories, _fixedExpenses, _unitOfWork);
    }

    private async Task<(Account Account, Category Category)> SeedAccountAndCategoryAsync()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            "Conta principal",
            Money.FromDecimal(1000.00m),
            InitialDate,
            CardBillingRule.Of(3, 12));
        await _accounts.AddAsync(account);

        var category = Category.Create(Guid.NewGuid(), account.Id, "Serviços");
        await _categories.AddAsync(category);

        return (account, category);
    }

    [Fact]
    public async Task ExecuteAsync_ValidCommandWithoutEnd_CreatesPersistsAndReturnsId()
    {
        var (account, category) = await SeedAccountAndCategoryAsync();

        var id = await _handler.ExecuteAsync(new RegisterFixedExpenseCommand(
            account.Id,
            2026,
            11,
            null,
            null,
            1200.00m,
            "  Aluguel  ",
            category.Id));

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);

        var saved = await _fixedExpenses.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(account.Id, saved.AccountId);
        Assert.Equal(YearMonth.Of(2026, 11), saved.StartMonth);
        Assert.Null(saved.EndMonth);
        Assert.Equal(Money.FromDecimal(1200.00m), saved.AmountIn(YearMonth.Of(2026, 11)));
        Assert.Equal("Aluguel", saved.Description);
        Assert.Equal(category.Id, saved.CategoryId);
    }

    [Fact]
    public async Task ExecuteAsync_StartMonthSameAsInitialBalanceMonth_IsAcceptedEvenIfInitialDateIsMidMonth()
    {
        var (account, category) = await SeedAccountAndCategoryAsync();

        var id = await _handler.ExecuteAsync(new RegisterFixedExpenseCommand(
            account.Id, 2026, 10, 2027, 3, 150.00m, "Internet", category.Id));

        var saved = await _fixedExpenses.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(YearMonth.Of(2026, 10), saved.StartMonth);
        Assert.Equal(YearMonth.Of(2027, 3), saved.EndMonth);
    }

    [Fact]
    public async Task ExecuteAsync_ArchivedCategory_ThrowsDomainExceptionAndDoesNotSave()
    {
        var (account, category) = await SeedAccountAndCategoryAsync();
        category.Archive();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new RegisterFixedExpenseCommand(
                account.Id, 2026, 10, null, null, 150.00m, "Internet", category.Id)));

        Assert.Empty(_fixedExpenses.Expenses);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_StartMonthBeforeInitialBalanceMonth_ThrowsDomainExceptionAndDoesNotSave()
    {
        var (account, category) = await SeedAccountAndCategoryAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new RegisterFixedExpenseCommand(
                account.Id, 2026, 9, null, null, 150.00m, "Internet", category.Id)));

        Assert.Empty(_fixedExpenses.Expenses);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Theory]
    [InlineData(2027, null)]
    [InlineData(null, 6)]
    public async Task ExecuteAsync_PartialEndMonth_ThrowsArgumentExceptionAndDoesNotSave(
        int? endYear, int? endMonth)
    {
        var (account, category) = await SeedAccountAndCategoryAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.ExecuteAsync(
            new RegisterFixedExpenseCommand(
                account.Id, 2026, 10, endYear, endMonth, 150.00m, "Internet", category.Id)));

        Assert.Empty(_fixedExpenses.Expenses);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFoundAndDoesNotSave()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new RegisterFixedExpenseCommand(
                unknownId, 2026, 10, null, null, 150.00m, "Internet", Guid.NewGuid())));

        Assert.Equal(nameof(Account), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_EndBeforeStart_ThrowsAndDoesNotSave()
    {
        var (account, category) = await SeedAccountAndCategoryAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _handler.ExecuteAsync(
            new RegisterFixedExpenseCommand(
                account.Id, 2026, 11, 2026, 10, 150.00m, "Internet", category.Id)));

        Assert.Empty(_fixedExpenses.Expenses);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
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
            () => new RegisterFixedExpenseHandler(null!, _categories, _fixedExpenses, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new RegisterFixedExpenseHandler(_accounts, null!, _fixedExpenses, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new RegisterFixedExpenseHandler(_accounts, _categories, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new RegisterFixedExpenseHandler(_accounts, _categories, _fixedExpenses, null!));
    }
}
