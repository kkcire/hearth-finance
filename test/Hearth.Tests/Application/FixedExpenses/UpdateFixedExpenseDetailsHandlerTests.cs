using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Application.FixedExpenses.UpdateFixedExpenseDetails;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.FixedExpenses;

public class UpdateFixedExpenseDetailsHandlerTests
{
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly InMemoryFixedExpenseRepository _fixedExpenses = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly UpdateFixedExpenseDetailsHandler _handler;

    public UpdateFixedExpenseDetailsHandlerTests()
    {
        _accounts.AddAsync(Account.Create(
            _accountId,
            FakeCurrentUser.DefaultUserId,
            "Conta",
            Money.Zero,
            new DateOnly(2026, 1, 1),
            CardBillingRule.Of(3, 12))).GetAwaiter().GetResult();
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new UpdateFixedExpenseDetailsHandler(_accountAccess, _categories, _fixedExpenses, _unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingExpense_UpdatesDescriptionAndCategoryAndSaves()
    {
        var accountId = _accountId;
        var services = Category.Create(Guid.NewGuid(), accountId, "Serviços");
        var education = Category.Create(Guid.NewGuid(), accountId, "Educação");
        await _categories.AddAsync(services);
        await _categories.AddAsync(education);

        var expense = FixedExpense.Create(
            Guid.NewGuid(),
            accountId,
            YearMonth.Of(2026, 1),
            null,
            Money.FromDecimal(1000.00m),
            "Aluguel",
            services.Id);
        await _fixedExpenses.AddAsync(expense);

        await _handler.ExecuteAsync(new UpdateFixedExpenseDetailsCommand(
            expense.Id, "  Faculdade  ", education.Id));

        Assert.Equal("Faculdade", expense.Description);
        Assert.Equal(education.Id, expense.CategoryId);
        Assert.Equal(1, _fixedExpenses.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_KeepingArchivedCategory_AllowsUpdatingDescription()
    {
        var accountId = _accountId;
        var services = Category.Create(Guid.NewGuid(), accountId, "Serviços");
        services.Archive();
        await _categories.AddAsync(services);

        var expense = FixedExpense.Create(
            Guid.NewGuid(),
            accountId,
            YearMonth.Of(2026, 1),
            null,
            Money.FromDecimal(1000.00m),
            "Aluguel",
            services.Id);
        await _fixedExpenses.AddAsync(expense);

        await _handler.ExecuteAsync(new UpdateFixedExpenseDetailsCommand(
            expense.Id, "Aluguel antigo", services.Id));

        Assert.Equal("Aluguel antigo", expense.Description);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ReassigningToArchivedCategory_ThrowsDomainException()
    {
        var accountId = _accountId;
        var services = Category.Create(Guid.NewGuid(), accountId, "Serviços");
        var archived = Category.Create(Guid.NewGuid(), accountId, "Antiga");
        archived.Archive();
        await _categories.AddAsync(services);
        await _categories.AddAsync(archived);

        var expense = FixedExpense.Create(
            Guid.NewGuid(),
            accountId,
            YearMonth.Of(2026, 1),
            null,
            Money.FromDecimal(1000.00m),
            "Aluguel",
            services.Id);
        await _fixedExpenses.AddAsync(expense);

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new UpdateFixedExpenseDetailsCommand(expense.Id, "Aluguel", archived.Id)));

        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownExpense_ThrowsNotFoundAndDoesNotSave()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new UpdateFixedExpenseDetailsCommand(unknownId, "Internet", Guid.NewGuid())));

        Assert.Equal(nameof(FixedExpense), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
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
            () => new UpdateFixedExpenseDetailsHandler(_accountAccess, null!, _fixedExpenses, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new UpdateFixedExpenseDetailsHandler(_accountAccess, _categories, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new UpdateFixedExpenseDetailsHandler(_accountAccess, _categories, _fixedExpenses, null!));
    }
}
