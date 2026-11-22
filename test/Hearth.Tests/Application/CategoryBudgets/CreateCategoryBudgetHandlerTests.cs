using Hearth.Application.Authorization;
using Hearth.Application.CategoryBudgets.CreateCategoryBudget;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.CategoryBudgets;

public class CreateCategoryBudgetHandlerTests
{
    private static readonly DateOnly InitialDate = new(2026, 10, 15);

    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly InMemoryCategoryBudgetRepository _budgets = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly CreateCategoryBudgetHandler _handler;

    public CreateCategoryBudgetHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new CreateCategoryBudgetHandler(_accountAccess, _categories, _budgets, _unitOfWork);
    }

    private async Task<(Account Account, Category Category)> SeedAsync()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta",
            Money.Zero,
            InitialDate,
            CardBillingRule.Of(3, 12));
        await _accounts.AddAsync(account);

        var category = Category.Create(Guid.NewGuid(), account.Id, "Transporte");
        await _categories.AddAsync(category);

        return (account, category);
    }

    [Fact]
    public async Task ExecuteAsync_ValidCommand_CreatesPersistsAndReturnsId()
    {
        var (account, category) = await SeedAsync();

        var id = await _handler.ExecuteAsync(new CreateCategoryBudgetCommand(
            account.Id, category.Id, 2026, 10, 2027, 6, 160.00m));

        var saved = await _budgets.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(account.Id, saved.AccountId);
        Assert.Equal(category.Id, saved.CategoryId);
        Assert.Equal(YearMonth.Of(2026, 10), saved.StartMonth);
        Assert.Equal(YearMonth.Of(2027, 6), saved.EndMonth);
        Assert.Equal(Money.FromDecimal(160.00m), saved.AmountIn(YearMonth.Of(2026, 10)));
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_DisjointPeriodsForSameCategory_AreAllowed()
    {
        var (account, category) = await SeedAsync();

        await _handler.ExecuteAsync(new CreateCategoryBudgetCommand(
            account.Id, category.Id, 2026, 10, 2026, 12, 160.00m));

        var secondId = await _handler.ExecuteAsync(new CreateCategoryBudgetCommand(
            account.Id, category.Id, 2027, 2, null, null, 200.00m));

        Assert.NotNull(await _budgets.GetByIdAsync(secondId));
        Assert.Equal(2, _budgets.Budgets.Count);
    }

    [Fact]
    public async Task ExecuteAsync_OverlappingPeriodForSameCategory_ThrowsDomainException()
    {
        var (account, category) = await SeedAsync();

        await _handler.ExecuteAsync(new CreateCategoryBudgetCommand(
            account.Id, category.Id, 2026, 10, 2026, 12, 160.00m));

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new CreateCategoryBudgetCommand(account.Id, category.Id, 2026, 12, null, null, 200.00m)));

        Assert.Single(_budgets.Budgets);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_StartMonthBeforeInitialBalanceMonth_ThrowsDomainException()
    {
        var (account, category) = await SeedAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new CreateCategoryBudgetCommand(account.Id, category.Id, 2026, 9, null, null, 160.00m)));

        Assert.Empty(_budgets.Budgets);
    }

    [Fact]
    public async Task ExecuteAsync_ArchivedCategory_ThrowsDomainException()
    {
        var (account, category) = await SeedAsync();
        category.Archive();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new CreateCategoryBudgetCommand(account.Id, category.Id, 2026, 10, null, null, 160.00m)));

        Assert.Empty(_budgets.Budgets);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new CreateCategoryBudgetCommand(unknownId, Guid.NewGuid(), 2026, 10, null, null, 160.00m)));

        Assert.Equal(nameof(Account), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new CreateCategoryBudgetHandler(null!, _categories, _budgets, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new CreateCategoryBudgetHandler(_accountAccess, null!, _budgets, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new CreateCategoryBudgetHandler(_accountAccess, _categories, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new CreateCategoryBudgetHandler(_accountAccess, _categories, _budgets, null!));
    }
}
