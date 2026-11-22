using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Application.Reports.GetBudgetStatus;
using Hearth.Application.Reports.GetExpensesByCategory;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Reports;

public class GetBudgetStatusHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly InMemoryCategoryBudgetRepository _budgets = new();
    private readonly InMemoryDirectExpenseRepository _directExpenses = new();
    private readonly InMemoryFixedExpenseRepository _fixedExpenses = new();
    private readonly InMemoryCreditCardPurchaseRepository _purchases = new();
    private readonly GetBudgetStatusHandler _handler;

    public GetBudgetStatusHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new GetBudgetStatusHandler(
            _accountAccess,
            _categories,
            _budgets,
            _directExpenses,
            _fixedExpenses,
            _purchases);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBudgetedCategoriesWithNamesTotalsAndConditionFiltering()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId, "Minha", Money.Zero, new DateOnly(2026, 1, 1), CardBillingRule.Of(3, 12));
        await _accounts.AddAsync(account);

        var transport = Category.Create(Guid.NewGuid(), account.Id, "Transporte");
        var food = Category.Create(Guid.NewGuid(), account.Id, "Alimentação");
        await _categories.AddAsync(transport);
        await _categories.AddAsync(food);

        await _budgets.AddAsync(CategoryBudget.Create(
            Guid.NewGuid(), account.Id, transport.Id, YearMonth.Of(2026, 1), null, Money.FromDecimal(160.00m)));
        await _budgets.AddAsync(CategoryBudget.Create(
            Guid.NewGuid(), account.Id, food.Id, YearMonth.Of(2026, 1), null, Money.FromDecimal(500.00m)));

        await _directExpenses.AddAsync(DirectExpense.Create(
            Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 5), Money.FromDecimal(120.00m), "Ônibus", transport.Id));
        await _purchases.AddAsync(CreditCardPurchase.Create(
            Guid.NewGuid(),
            account.Id,
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(300.00m),
            1,
            1,
            account.BillingRule,
            PurchaseCondition.Others,
            "Terceiro",
            food.Id));

        var ownOnly = await _handler.ExecuteAsync(new GetBudgetStatusQuery(
            account.Id,
            2026,
            11,
            IncludedConditions: new[] { new PurchaseConditionFilter(PurchaseConditionKind.Own) }));

        Assert.Equal(660.00m, ownOnly.TotalBudgeted);
        Assert.Equal(120.00m, ownOnly.TotalSpent);
        Assert.Equal(540.00m, ownOnly.TotalRemaining);
        Assert.Equal(2, ownOnly.Items.Count);

        Assert.Equal("Transporte", ownOnly.Items[0].CategoryName);
        Assert.Equal(75.00m, ownOnly.Items[0].UsagePercentage);
        Assert.False(ownOnly.Items[0].IsExceeded);

        Assert.Equal("Alimentação", ownOnly.Items[1].CategoryName);
        Assert.Equal(0.00m, ownOnly.Items[1].SpentAmount);
        Assert.Equal(0.00m, ownOnly.Items[1].UsagePercentage);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.ExecuteAsync(new GetBudgetStatusQuery(unknownId, 2026, 11)));

        Assert.Equal(nameof(Account), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new GetBudgetStatusHandler(null!, _categories, _budgets, _directExpenses, _fixedExpenses, _purchases));
        Assert.Throws<ArgumentNullException>(() => new GetBudgetStatusHandler(_accountAccess, null!, _budgets, _directExpenses, _fixedExpenses, _purchases));
        Assert.Throws<ArgumentNullException>(() => new GetBudgetStatusHandler(_accountAccess, _categories, null!, _directExpenses, _fixedExpenses, _purchases));
        Assert.Throws<ArgumentNullException>(() => new GetBudgetStatusHandler(_accountAccess, _categories, _budgets, null!, _fixedExpenses, _purchases));
        Assert.Throws<ArgumentNullException>(() => new GetBudgetStatusHandler(_accountAccess, _categories, _budgets, _directExpenses, null!, _purchases));
        Assert.Throws<ArgumentNullException>(() => new GetBudgetStatusHandler(_accountAccess, _categories, _budgets, _directExpenses, _fixedExpenses, null!));
    }
}
