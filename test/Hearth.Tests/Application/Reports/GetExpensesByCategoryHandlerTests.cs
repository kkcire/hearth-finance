using Hearth.Application.Exceptions;
using Hearth.Application.Reports.GetExpensesByCategory;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Reports;

public class GetExpensesByCategoryHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly InMemoryDirectExpenseRepository _directExpenses = new();
    private readonly InMemoryFixedExpenseRepository _fixedExpenses = new();
    private readonly InMemoryCreditCardPurchaseRepository _purchases = new();
    private readonly GetExpensesByCategoryHandler _handler;

    public GetExpensesByCategoryHandlerTests()
    {
        _handler = new GetExpensesByCategoryHandler(
            _accounts,
            _directExpenses,
            _fixedExpenses,
            _purchases);
    }

    private async Task<Account> SeedAccountAsync(string name = "Conta")
    {
        var account = Account.Create(
            Guid.NewGuid(),
            name,
            Money.Zero,
            new DateOnly(2026, 1, 1),
            CardBillingRule.Of(3, 12));
        await _accounts.AddAsync(account);
        return account;
    }

    [Fact]
    public async Task ExecuteAsync_WithDefaultFilters_ReturnsAllSourcesGroupedByCategory()
    {
        var account = await SeedAccountAsync("Minha");
        var otherAccount = await SeedAccountAsync("Outra");

        var direct = DirectExpense.Create(
            Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 10), Money.FromDecimal(120.00m), "Mercado", ExpenseCategory.Food);
        var otherDirect = DirectExpense.Create(
            Guid.NewGuid(), otherAccount.Id, new DateOnly(2026, 11, 10), Money.FromDecimal(900.00m), "Outro", ExpenseCategory.Food);

        var rent = FixedExpense.Create(
            Guid.NewGuid(), account.Id, YearMonth.Of(2026, 1), null, Money.FromDecimal(1000.00m), "Aluguel", ExpenseCategory.Services);

        var ownPurchase = CreditCardPurchase.Create(
            Guid.NewGuid(),
            account.Id,
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(80.00m),
            1,
            1,
            account.BillingRule,
            PurchaseCondition.Own,
            "Restaurante",
            ExpenseCategory.Food);

        var othersPurchase = CreditCardPurchase.Create(
            Guid.NewGuid(),
            account.Id,
            new DateOnly(2026, 10, 11),
            Money.FromDecimal(300.00m),
            1,
            1,
            account.BillingRule,
            PurchaseCondition.Others,
            "Compra de terceiro",
            ExpenseCategory.Shopping);

        await _directExpenses.AddAsync(direct);
        await _directExpenses.AddAsync(otherDirect);
        await _fixedExpenses.AddAsync(rent);
        await _purchases.AddAsync(ownPurchase);
        await _purchases.AddAsync(othersPurchase);

        var dto = await _handler.ExecuteAsync(new GetExpensesByCategoryQuery(account.Id, 2026, 11));

        Assert.Equal(account.Id, dto.AccountId);
        Assert.Equal(2026, dto.Year);
        Assert.Equal(11, dto.Month);
        Assert.Equal(120.00m, dto.DirectExpenses);
        Assert.Equal(1000.00m, dto.FixedExpenses);
        Assert.Equal(380.00m, dto.CardInstallments);
        Assert.Equal(1500.00m, dto.Total);

        Assert.Equal(3, dto.Categories.Count);
        Assert.Equal(ExpenseCategory.Services, dto.Categories[0].Category);
        Assert.Equal(1000.00m, dto.Categories[0].Total);

        Assert.Equal(ExpenseCategory.Shopping, dto.Categories[1].Category);
        Assert.Equal(300.00m, dto.Categories[1].CardInstallments);

        Assert.Equal(ExpenseCategory.Food, dto.Categories[2].Category);
        Assert.Equal(120.00m, dto.Categories[2].DirectExpenses);
        Assert.Equal(80.00m, dto.Categories[2].CardInstallments);
        Assert.Equal(200.00m, dto.Categories[2].Total);
    }

    [Fact]
    public async Task ExecuteAsync_WithOrthogonalSourceAndConditionFilters_FiltersCardOnlyForOwnAndCustom()
    {
        var account = await SeedAccountAsync();

        var direct = DirectExpense.Create(
            Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 5), Money.FromDecimal(50.00m), "Pix", ExpenseCategory.Food);
        var fixedExpense = FixedExpense.Create(
            Guid.NewGuid(), account.Id, YearMonth.Of(2026, 1), null, Money.FromDecimal(100.00m), "Internet", ExpenseCategory.Services);

        var own = CreditCardPurchase.Create(
            Guid.NewGuid(),
            account.Id,
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(200.00m),
            1,
            1,
            account.BillingRule,
            PurchaseCondition.Own,
            "Propria",
            ExpenseCategory.Food);
        var trip = CreditCardPurchase.Create(
            Guid.NewGuid(),
            account.Id,
            new DateOnly(2026, 10, 11),
            Money.FromDecimal(150.00m),
            1,
            1,
            account.BillingRule,
            PurchaseCondition.Custom("Viagem"),
            "Hotel",
            ExpenseCategory.Entertainment);
        var others = CreditCardPurchase.Create(
            Guid.NewGuid(),
            account.Id,
            new DateOnly(2026, 10, 12),
            Money.FromDecimal(400.00m),
            1,
            1,
            account.BillingRule,
            PurchaseCondition.Others,
            "Terceiro",
            ExpenseCategory.Shopping);

        await _directExpenses.AddAsync(direct);
        await _fixedExpenses.AddAsync(fixedExpense);
        await _purchases.AddAsync(own);
        await _purchases.AddAsync(trip);
        await _purchases.AddAsync(others);

        var dto = await _handler.ExecuteAsync(new GetExpensesByCategoryQuery(
            account.Id,
            2026,
            11,
            IncludeDirectExpenses: false,
            IncludeFixedExpenses: false,
            IncludeCardInstallments: true,
            IncludedConditions: new[]
            {
                new PurchaseConditionFilter(PurchaseConditionKind.Own),
                new PurchaseConditionFilter(PurchaseConditionKind.Custom, "Viagem"),
            }));

        Assert.Equal(0.00m, dto.DirectExpenses);
        Assert.Equal(0.00m, dto.FixedExpenses);
        Assert.Equal(350.00m, dto.CardInstallments);
        Assert.Equal(350.00m, dto.Total);
        Assert.Equal(2, dto.Categories.Count);
        Assert.Equal(ExpenseCategory.Food, dto.Categories[0].Category);
        Assert.Equal(ExpenseCategory.Entertainment, dto.Categories[1].Category);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidConditionFilter_Throws()
    {
        var account = await SeedAccountAsync();

        // Own with a non-empty name is invalid.
        await Assert.ThrowsAsync<ArgumentException>(() => _handler.ExecuteAsync(
            new GetExpensesByCategoryQuery(
                account.Id,
                2026,
                11,
                IncludedConditions: new[]
                {
                    new PurchaseConditionFilter(PurchaseConditionKind.Own, "ShouldNotHaveName"),
                })));

        // Custom with null/blank name is invalid.
        await Assert.ThrowsAsync<ArgumentException>(() => _handler.ExecuteAsync(
            new GetExpensesByCategoryQuery(
                account.Id,
                2026,
                11,
                IncludedConditions: new[]
                {
                    new PurchaseConditionFilter(PurchaseConditionKind.Custom, "   "),
                })));

        // Null entry in condition filter list is invalid.
        await Assert.ThrowsAsync<ArgumentException>(() => _handler.ExecuteAsync(
            new GetExpensesByCategoryQuery(
                account.Id,
                2026,
                11,
                IncludedConditions: new PurchaseConditionFilter[] { null! })));
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.ExecuteAsync(new GetExpensesByCategoryQuery(unknownId, 2026, 11)));

        Assert.Equal(nameof(Account), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidMonth_Throws()
    {
        var account = await SeedAccountAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _handler.ExecuteAsync(new GetExpensesByCategoryQuery(account.Id, 2026, 13)));
    }

    [Fact]
    public async Task ExecuteAsync_NullQuery_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.ExecuteAsync(null!));
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GetExpensesByCategoryHandler(null!, _directExpenses, _fixedExpenses, _purchases));
        Assert.Throws<ArgumentNullException>(() =>
            new GetExpensesByCategoryHandler(_accounts, null!, _fixedExpenses, _purchases));
        Assert.Throws<ArgumentNullException>(() =>
            new GetExpensesByCategoryHandler(_accounts, _directExpenses, null!, _purchases));
        Assert.Throws<ArgumentNullException>(() =>
            new GetExpensesByCategoryHandler(_accounts, _directExpenses, _fixedExpenses, null!));
    }
}
