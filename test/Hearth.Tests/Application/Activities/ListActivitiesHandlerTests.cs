using Hearth.Application;
using Hearth.Application.Activities.ListActivities;
using Hearth.Application.Authorization;
using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace Hearth.Tests.Application.Activities;

public sealed class ListActivitiesHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly InMemoryIncomeRepository _incomes = new();
    private readonly InMemoryFixedIncomeRepository _fixedIncomes = new();
    private readonly InMemoryDirectExpenseRepository _directExpenses = new();
    private readonly InMemoryFixedExpenseRepository _fixedExpenses = new();
    private readonly InMemoryCreditCardPurchaseRepository _purchases = new();
    private readonly FakeClock _clock = new(new DateOnly(2026, 11, 25));
    private readonly ListActivitiesHandler _handler;

    public ListActivitiesHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new ListActivitiesHandler(
            _accountAccess,
            _categories,
            _incomes,
            _fixedIncomes,
            _directExpenses,
            _fixedExpenses,
            _purchases,
            _clock);
    }

    [Fact]
    public async Task ExecuteAsync_AggregatesAllMovementTypesWithInstallmentsAndFiltersCleanly()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta Principal",
            Money.FromDecimal(1000m),
            TestTime.UtcOn(new DateOnly(2026, 11, 1)),
            isInitialBalanceConfirmed: true);
        await _accounts.AddAsync(account);

        var cardId = Guid.NewGuid();
        var rule = CardBillingRule.Of(2, 14);

        var income = Income.Create(Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 5), Money.FromDecimal(3000m), "Salário");
        await _incomes.AddAsync(income);
        var futureIncome = Income.Create(Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 28), Money.FromDecimal(400m), "Freela futuro");
        await _incomes.AddAsync(futureIncome);

        var fixedIncome = FixedIncome.Create(
            Guid.NewGuid(), account.Id, YearMonth.Of(2026, 11), null, 10, Money.FromDecimal(1200m), "Aluguel Fixo");
        fixedIncome.ConfirmReceipt(YearMonth.Of(2026, 11), new DateOnly(2026, 11, 11), Money.FromDecimal(1250m), _clock.Today);
        await _fixedIncomes.AddAsync(fixedIncome);

        await _directExpenses.AddAsync(DirectExpense.Create(
            Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 10), Money.FromDecimal(150m), "Mercado", SystemCategories.FoodId));

        var fixedExpense = FixedExpense.Create(
            Guid.NewGuid(), account.Id, YearMonth.Of(2026, 11), null, Money.FromDecimal(120m), "Internet", SystemCategories.ServicesId);
        await _fixedExpenses.AddAsync(fixedExpense);

        // Purchase on Oct 10 -> 3x 100: Nov 14 (1/3, Paid), Dec 14 (2/3, Open), Jan 14 (3/3, Open)
        await _purchases.AddAsync(CreditCardPurchase.Create(
            Guid.NewGuid(), account.Id, cardId, new DateOnly(2026, 10, 10), Money.FromDecimal(300m), 3, 1, rule,
            SystemPurchaseConditions.OwnId, "Monitor", SystemCategories.ShoppingId));

        // Others purchase due Dec 14
        await _purchases.AddAsync(CreditCardPurchase.Create(
            Guid.NewGuid(), account.Id, cardId, new DateOnly(2026, 11, 10), Money.FromDecimal(200m), 1, 1, rule,
            SystemPurchaseConditions.OthersId, "Presente", SystemCategories.ShoppingId));

        // Filter Nov 2026 -> 6 items (Salário, Freela futuro, Aluguel Fixo, Mercado, Internet, Monitor 1/3)
        var novItems = await _handler.ExecuteAsync(new ListActivitiesQuery(account.Id, 2026, 11));
        Assert.Equal(6, novItems.Count);
        Assert.True(novItems.Zip(novItems.Skip(1)).All(pair => pair.First.Date >= pair.Second.Date));

        var fixedIncomeNov = Assert.Single(novItems, i => i.Description == "Aluguel Fixo");
        Assert.Equal(1250m, fixedIncomeNov.Amount);
        Assert.Equal(new DateOnly(2026, 11, 11), fixedIncomeNov.Date);
        Assert.Equal("Efetivado", fixedIncomeNov.StatusLabel);
        Assert.Equal(ListActivitiesHandler.FixedIncomeType, fixedIncomeNov.MovementType);

        var decFixedIncomes = await _handler.ExecuteAsync(new ListActivitiesQuery(
            account.Id, 2026, 12, MovementType: ListActivitiesHandler.FixedIncomeType));
        var fixedIncomeDec = Assert.Single(decFixedIncomes);
        Assert.Equal(1200m, fixedIncomeDec.Amount);
        Assert.Equal("Previsto", fixedIncomeDec.StatusLabel);

        var salaryNov = Assert.Single(novItems, i => i.Description == "Salário");
        Assert.Equal("Efetivado", salaryNov.StatusLabel);

        var futureIncomeNov = Assert.Single(novItems, i => i.Description == "Freela futuro");
        Assert.Equal("Previsto", futureIncomeNov.StatusLabel);

        var monitorNov = Assert.Single(novItems, i => i.Description == "Monitor");
        Assert.Equal("1/3", monitorNov.InstallmentInfo);
        Assert.Equal(-100m, monitorNov.Amount);
        Assert.Equal("Efetivado", monitorNov.StatusLabel);
        Assert.Equal("Compras", monitorNov.CategoryName);

        var internetNov = Assert.Single(novItems, i => i.Description == "Internet");
        Assert.Equal("Previsto", internetNov.StatusLabel);

        // Filter Dec 2026 with ConditionId = Own -> excludes "Presente" (Others)
        var decOwn = await _handler.ExecuteAsync(new ListActivitiesQuery(
            account.Id, 2026, 12, ConditionId: SystemPurchaseConditions.OwnId, MovementType: ListActivitiesHandler.CardPurchaseType));
        var monitorDec = Assert.Single(decOwn);
        Assert.Equal("Monitor", monitorDec.Description);
        Assert.Equal("2/3", monitorDec.InstallmentInfo);
        Assert.Equal("Previsto", monitorDec.StatusLabel);

        // Filter All Months (Year = null, Month = null) -> includes all 3 Monitor installments + Presente + 2 Incomes + 1 FixedIncome + Direct + Fixed
        var allItems = await _handler.ExecuteAsync(new ListActivitiesQuery(account.Id));
        Assert.Equal(9, allItems.Count);
    }

    [Fact]
    public async Task ExecuteAsync_ExcludesInstallmentsDueBeforeInitialBalanceDate()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta Principal",
            Money.FromDecimal(500m),
            TestTime.UtcOn(new DateOnly(2026, 11, 20)),
            isInitialBalanceConfirmed: true);
        await _accounts.AddAsync(account);

        var rule = CardBillingRule.Of(2, 14);
        // Account TrackingStartDate = 2026-11-01 (created Nov 20).
        // Purchased Sep 10 -> Installment 1 due Oct 14 (< Nov 1 -> excluded), Installment 2 due Nov 14 (>= Nov 1 -> included)
        await _purchases.AddAsync(CreditCardPurchase.Create(
            Guid.NewGuid(), account.Id, Guid.NewGuid(), new DateOnly(2026, 9, 10), Money.FromDecimal(200m), 2, 1, rule,
            SystemPurchaseConditions.OwnId, "Fone", SystemCategories.ShoppingId));

        var items = await _handler.ExecuteAsync(new ListActivitiesQuery(account.Id));
        var single = Assert.Single(items);
        Assert.Equal(new DateOnly(2026, 11, 14), single.Date);
        Assert.Equal("2/2", single.InstallmentInfo);
    }

    [Fact]
    public void DependencyInjection_RegistersListActivitiesHandler()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        Assert.Contains(services, d => d.ServiceType == typeof(ListActivitiesHandler));
    }
}
