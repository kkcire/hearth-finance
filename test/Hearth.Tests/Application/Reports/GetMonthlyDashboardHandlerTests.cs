using Hearth.Application;
using Hearth.Application.Authorization;
using Hearth.Application.Reports.GetMonthlyDashboard;
using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace Hearth.Tests.Application.Reports;

public sealed class GetMonthlyDashboardHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryCreditCardRepository _creditCards = new();
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly InMemoryCategoryBudgetRepository _budgets = new();
    private readonly InMemoryIncomeRepository _incomes = new();
    private readonly InMemoryFixedIncomeRepository _fixedIncomes = new();
    private readonly InMemoryDirectExpenseRepository _directExpenses = new();
    private readonly InMemoryFixedExpenseRepository _fixedExpenses = new();
    private readonly InMemoryCreditCardPurchaseRepository _purchases = new();
    private readonly FakeClock _clock = new(new DateOnly(2026, 11, 24));
    private readonly GetMonthlyDashboardHandler _handler;

    public GetMonthlyDashboardHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new GetMonthlyDashboardHandler(
            _accountAccess,
            _creditCards,
            _categories,
            _budgets,
            _incomes,
            _fixedIncomes,
            _directExpenses,
            _fixedExpenses,
            _purchases,
            _clock);
    }

    [Fact]
    public async Task ExecuteAsync_MapsAllDashboardBlocksWithStrictMonthIsolationAndConditionFilter()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta Principal",
            Money.FromDecimal(260.39m),
            TestTime.UtcOn(new DateOnly(2026, 10, 1)),
            isInitialBalanceConfirmed: true);
        await _accounts.AddAsync(account);

        var rule = CardBillingRule.Of(2, 14);
        var card = CreditCard.Create(Guid.NewGuid(), account.Id, rule, "Cartão BB", Money.FromDecimal(7438m));
        await _creditCards.AddAsync(card);

        // Dec 2026: Income 2275, Fixed 470.99, Direct 300, Card Own 265.99 (plus 2 future installments of 265.99), Card Others 500
        await _incomes.AddAsync(Income.Create(Guid.NewGuid(), account.Id, new DateOnly(2026, 12, 5), Money.FromDecimal(2275m), "Salário Dez"));
        await _fixedExpenses.AddAsync(FixedExpense.Create(Guid.NewGuid(), account.Id, YearMonth.Of(2026, 12), null, Money.FromDecimal(470.99m), "Internet e Luz", SystemCategories.ServicesId));
        await _directExpenses.AddAsync(DirectExpense.Create(Guid.NewGuid(), account.Id, new DateOnly(2026, 12, 8), Money.FromDecimal(300m), "Supermercado", SystemCategories.FoodId));

        // 3x of 265.99 = 797.97 purchased on Nov 10 -> Due Dec 14, Jan 14, Feb 14
        await _purchases.AddAsync(CreditCardPurchase.Create(
            Guid.NewGuid(), account.Id, card.Id, new DateOnly(2026, 11, 10), Money.FromDecimal(797.97m), 3, 1, rule,
            SystemPurchaseConditions.OwnId, "Monitor", SystemCategories.ShoppingId));

        // Others purchase 500 due Dec 14
        await _purchases.AddAsync(CreditCardPurchase.Create(
            Guid.NewGuid(), account.Id, card.Id, new DateOnly(2026, 11, 12), Money.FromDecimal(500m), 1, 1, rule,
            SystemPurchaseConditions.OthersId, "Presente Outros", SystemCategories.ShoppingId));

        // Jan 2027 income 2000 (isolated from Dec's unclosed surplus!)
        await _incomes.AddAsync(Income.Create(Guid.NewGuid(), account.Id, new DateOnly(2027, 1, 5), Money.FromDecimal(2000m), "Salário Jan"));

        // Budget for Food: 600 -> 300 / 600 = 50%
        await _budgets.AddAsync(CategoryBudget.Create(
            Guid.NewGuid(), account.Id, SystemCategories.FoodId, YearMonth.Of(2026, 12), null, Money.FromDecimal(600m)));

        var dto = await _handler.ExecuteAsync(new GetMonthlyDashboardQuery(
            account.Id,
            YearMonth.Of(2026, 12),
            ConditionId: SystemPurchaseConditions.OwnId));

        // 1. Balance (strict real cash on 24/11/2026, unaffected by future Dec items)
        Assert.Equal(260.39m, dto.Balance.Amount);
        Assert.Equal(new DateOnly(2026, 11, 24), dto.Balance.AsOfDate);

        // 2. Current Invoice (Own only: Dec cycle = 265.99, Jan+Feb future = 531.98)
        Assert.Equal(YearMonth.Of(2026, 12), dto.CurrentInvoice.BillingMonth);
        Assert.Equal(265.99m, dto.CurrentInvoice.CycleAmount);
        Assert.Equal(new DateOnly(2026, 12, 14), dto.CurrentInvoice.DueDate);
        Assert.Equal(new DateOnly(2026, 12, 2), dto.CurrentInvoice.ClosingDate);
        Assert.Equal(InvoiceStatus.Open, dto.CurrentInvoice.Status);
        Assert.Equal(531.98m, dto.CurrentInvoice.FutureInstallmentsTotal);
        Assert.True(dto.CurrentInvoice.HasActiveCard);

        // 3. Current Month Flow (isolated Dec 2026: 2275 - (470.99 + 300 + 265.99 = 1036.98) = 1238.02)
        Assert.Equal(2275.00m, dto.CurrentMonthFlow.MonthlyIncome);
        Assert.Equal(1036.98m, dto.CurrentMonthFlow.TotalExpenses);
        Assert.Equal(1238.02m, dto.CurrentMonthFlow.MonthlyResult);
        Assert.Equal(54.4m, dto.CurrentMonthFlow.SavingsRatePercentage);
        Assert.Equal(BreakEvenStatus.Healthy, dto.CurrentMonthFlow.BreakEvenStatus);

        // 4. Monthly Result History clamped to InitialBalanceDate (Oct..Dec 2026 -> 3 months, no empty pre-account months)
        Assert.Equal(3, dto.MonthlyResultHistory.Count);
        Assert.Equal(YearMonth.Of(2026, 10), dto.MonthlyResultHistory[0].YearMonth);
        Assert.Equal(BreakEvenStatus.NoActivity, dto.MonthlyResultHistory[0].BreakEvenStatus);
        Assert.False(dto.MonthlyResultHistory[0].IsProjection);
        Assert.Equal(YearMonth.Of(2026, 12), dto.MonthlyResultHistory[^1].YearMonth);
        Assert.True(dto.MonthlyResultHistory[^1].IsProjection);

        // 5. Category Spending descending (Services 470.99, Food 300.00 w/ 50% budget, Shopping 265.99)
        Assert.Equal(3, dto.CategorySpending.Count);
        Assert.Equal("Serviços", dto.CategorySpending[0].CategoryName);
        Assert.Equal(470.99m, dto.CategorySpending[0].AmountSpent);
        var food = dto.CategorySpending.Single(c => c.CategoryName == "Alimentação");
        Assert.Equal(50.0m, food.BudgetUsagePercentage);
        Assert.StartsWith("#", food.HexColor);
        var foodEntry = Assert.Single(food.Entries!);
        Assert.Equal("Supermercado", foodEntry.Description);
        Assert.Equal("Gasto direto", foodEntry.OriginLabel);
        Assert.Equal(300.00m, foodEntry.Amount);

        var shopping = dto.CategorySpending.Single(c => c.CategoryName == "Compras");
        var shoppingEntry = Assert.Single(shopping.Entries!);
        Assert.Equal("Monitor", shoppingEntry.Description);
        Assert.Equal("Cartão", shoppingEntry.OriginLabel);
        Assert.Equal("1/3", shoppingEntry.InstallmentLabel);

        // 6. Recent Activities scoped to DirectExpenses, FixedExpenses, and CardPurchases made IN Dec 2026 (excludes incomes and Nov purchases)
        Assert.NotEmpty(dto.RecentActivities);
        Assert.DoesNotContain(dto.RecentActivities, a => a.MovementType == "Income");
        Assert.DoesNotContain(dto.RecentActivities, a => a.Description.Contains("Presente Outros"));
        Assert.DoesNotContain(dto.RecentActivities, a => a.Description.StartsWith("Monitor")); // purchased in Nov, not Dec
        Assert.Contains(dto.RecentActivities, a => a.Description == "Supermercado" && a.MovementType == "DirectExpense");
        Assert.Contains(dto.RecentActivities, a => a.Description == "Internet e Luz" && a.MovementType == "FixedExpense");
        Assert.True(dto.RecentActivities.Zip(dto.RecentActivities.Skip(1)).All(pair => pair.First.Date >= pair.Second.Date));

        // 7. Next Month Estimate (Jan 2027 strictly isolated; balance projection is null because Dec != current month Nov and condition is filtered)
        Assert.NotNull(dto.NextMonthEstimate);
        Assert.Equal(YearMonth.Of(2027, 1), dto.NextMonthEstimate!.NextYearMonth);
        Assert.Equal(2000.00m, dto.NextMonthEstimate.EstimatedIncome);
        Assert.Equal(470.99m, dto.NextMonthEstimate.EstimatedFixedExpenses);
        Assert.Equal(265.99m, dto.NextMonthEstimate.EstimatedCardInstallments);
        Assert.Equal(0m, dto.NextMonthEstimate.EstimatedDirectExpenses);
        Assert.Equal(736.98m, dto.NextMonthEstimate.EstimatedCommitments);
        Assert.Equal(1263.02m, dto.NextMonthEstimate.ProjectedResult);
        Assert.Equal(63.2m, dto.NextMonthEstimate.EstimatedSavingsRatePercentage);
        Assert.Null(dto.NextMonthEstimate.ProjectedOpeningBalance);
        Assert.Null(dto.NextMonthEstimate.ProjectedEndingBalance);

        // Viewing current calendar month (Nov 2026) under "Todas" maps ProjectedOpeningBalance & ProjectedEndingBalance for Dec 2026
        // RealBalance(Nov 24) = 260.39, no pending Nov items -> Opening(Dec) = 260.39
        // Dec total (all conditions): +2275 - (470.99 fixed + 300 direct + 265.99 own card + 500 others card) = +738.02
        // Ending(Dec) = 260.39 + 738.02 = 998.41
        var currentAllDto = await _handler.ExecuteAsync(new GetMonthlyDashboardQuery(
            account.Id,
            YearMonth.Of(2026, 11),
            ConditionId: null));
        Assert.NotNull(currentAllDto.NextMonthEstimate);
        Assert.Equal(260.39m, currentAllDto.NextMonthEstimate!.ProjectedOpeningBalance);
        Assert.Equal(998.41m, currentAllDto.NextMonthEstimate.ProjectedEndingBalance);
    }

    [Fact]
    public async Task ExecuteAsync_CapsFutureBalanceAsOfDateAtToday()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta Principal",
            Money.FromDecimal(500m),
            TestTime.UtcOn(new DateOnly(2026, 10, 1)),
            isInitialBalanceConfirmed: true);
        await _accounts.AddAsync(account);

        await _directExpenses.AddAsync(DirectExpense.Create(
            Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 29), Money.FromDecimal(120m), "Futuro", SystemCategories.FoodId));

        // Requesting a future BalanceAsOfDate (Dec 31) must be capped to today (Nov 24)
        var dto = await _handler.ExecuteAsync(new GetMonthlyDashboardQuery(
            account.Id,
            YearMonth.Of(2026, 11),
            BalanceAsOfDate: new DateOnly(2026, 12, 31)));

        Assert.Equal(new DateOnly(2026, 11, 24), dto.Balance.AsOfDate);
        Assert.Equal(500m, dto.Balance.Amount);
    }

    [Fact]
    public async Task ExecuteAsync_RecentActivities_IncludesOnlyDirectFixedAndCardPurchasesMadeInSelectedMonth()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta Principal",
            Money.FromDecimal(1000m),
            TestTime.UtcOn(new DateOnly(2026, 10, 1)),
            isInitialBalanceConfirmed: true);
        await _accounts.AddAsync(account);

        var rule = CardBillingRule.Of(2, 14);
        var card = CreditCard.Create(Guid.NewGuid(), account.Id, rule, "Cartão", Money.FromDecimal(5000m));
        await _creditCards.AddAsync(card);

        // Income in Nov -> excluded from Dashboard RecentActivities (only expenses & purchases made in that month)
        var income = Income.Create(Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 3), Money.FromDecimal(1500m), "Reembolso");
        await _incomes.AddAsync(income);

        // Card purchase made on Nov 10 (due Dec 14) -> appears in Nov recent activities (and NOT in Dec!)
        await _purchases.AddAsync(CreditCardPurchase.Create(
            Guid.NewGuid(), account.Id, card.Id, new DateOnly(2026, 11, 10), Money.FromDecimal(300m), 3, 1, rule,
            SystemPurchaseConditions.OwnId, "Teclado", SystemCategories.ShoppingId));

        // Future direct expense on Nov 29 -> appears AFTER realized Nov 10 item
        await _directExpenses.AddAsync(DirectExpense.Create(
            Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 29), Money.FromDecimal(90m), "Agendado", SystemCategories.FoodId));

        var novDto = await _handler.ExecuteAsync(new GetMonthlyDashboardQuery(account.Id, YearMonth.Of(2026, 11)));
        Assert.DoesNotContain(novDto.RecentActivities, a => a.Description == "Reembolso");
        Assert.Contains(novDto.RecentActivities, a => a.Description == "Teclado (3x)" && a.Date == new DateOnly(2026, 11, 10) && a.Amount == -300m);
        Assert.Equal("Agendado", novDto.RecentActivities[^1].Description);
        Assert.Equal("Previsto", novDto.RecentActivities[^1].StatusLabel);

        var decDto = await _handler.ExecuteAsync(new GetMonthlyDashboardQuery(account.Id, YearMonth.Of(2026, 12)));
        Assert.DoesNotContain(decDto.RecentActivities, a => a.Description.StartsWith("Teclado"));
    }

    [Fact]
    public async Task ExecuteAsync_Option1SavingsRate_UsesOwnMonthlyIncomeWithoutThirdPartyReimbursementDilution()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta Principal",
            Money.Zero,
            TestTime.UtcOn(new DateOnly(2026, 10, 1)),
            isInitialBalanceConfirmed: true);
        await _accounts.AddAsync(account);

        var rule = CardBillingRule.Of(2, 14);
        var card = CreditCard.Create(Guid.NewGuid(), account.Id, rule, "Cartão", Money.FromDecimal(5000m));
        await _creditCards.AddAsync(card);

        var paiId = Guid.NewGuid();
        var ownIncome = Income.Create(Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 5), Money.FromDecimal(2000m), "Salário", SystemPurchaseConditions.OwnId);
        var paiReimbursement = Income.Create(Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 10), Money.FromDecimal(950m), "Reembolso Pai", paiId);
        await _incomes.AddAsync(ownIncome);
        await _incomes.AddAsync(paiReimbursement);

        await _directExpenses.AddAsync(DirectExpense.Create(
            Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 8), Money.FromDecimal(300m), "Mercado", SystemCategories.FoodId));
        // Card purchase made Oct 15 -> closes Nov 2, due Nov 14 (paid on Nov 24)
        await _purchases.AddAsync(CreditCardPurchase.Create(
            Guid.NewGuid(), account.Id, card.Id, new DateOnly(2026, 10, 15), Money.FromDecimal(950m), 1, 1, rule,
            paiId, "Compra Pai", SystemCategories.ShoppingId));

        // Under "Todas" (ConditionId = null): gross movement visible, SavingsRate = (2950 - 1250) / 2000 = 85.0%
        var allDto = await _handler.ExecuteAsync(new GetMonthlyDashboardQuery(account.Id, YearMonth.Of(2026, 11), ConditionId: null));
        Assert.Equal(2950m, allDto.CurrentMonthFlow.MonthlyIncome);
        Assert.Equal(2000m, allDto.CurrentMonthFlow.OwnMonthlyIncome);
        Assert.Equal(1250m, allDto.CurrentMonthFlow.TotalExpenses);
        Assert.Equal(1700m, allDto.CurrentMonthFlow.MonthlyResult);
        Assert.Equal(85.0m, allDto.CurrentMonthFlow.SavingsRatePercentage);
        Assert.Equal(1700m, allDto.Balance.Amount);
        Assert.Equal(2, allDto.CurrentMonthFlow.IncomeEntries!.Count);

        // Under "Própria": only own income & own expenses -> Receita 2000, Despesa 300, Resultado 1700, Taxa 85.0%, SALDO still 1700
        var ownDto = await _handler.ExecuteAsync(new GetMonthlyDashboardQuery(account.Id, YearMonth.Of(2026, 11), ConditionId: SystemPurchaseConditions.OwnId));
        Assert.Equal(2000m, ownDto.CurrentMonthFlow.MonthlyIncome);
        Assert.Equal(2000m, ownDto.CurrentMonthFlow.OwnMonthlyIncome);
        Assert.Equal(300m, ownDto.CurrentMonthFlow.TotalExpenses);
        Assert.Equal(1700m, ownDto.CurrentMonthFlow.MonthlyResult);
        Assert.Equal(85.0m, ownDto.CurrentMonthFlow.SavingsRatePercentage);
        Assert.Equal(1700m, ownDto.Balance.Amount);
        var onlyOwnIncomeEntry = Assert.Single(ownDto.CurrentMonthFlow.IncomeEntries!);
        Assert.Equal("Salário", onlyOwnIncomeEntry.Description);
    }

    [Fact]
    public async Task ExecuteAsync_IncludesFixedIncomesInBalanceFlowIncomeEntriesAndNextMonthEstimate()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta Principal",
            Money.FromDecimal(500m),
            TestTime.UtcOn(new DateOnly(2026, 10, 1)),
            isInitialBalanceConfirmed: true);
        await _accounts.AddAsync(account);

        var salary = FixedIncome.Create(
            Guid.NewGuid(), account, YearMonth.Of(2026, 10), null, 5, Money.FromDecimal(4000m), "Salário Fixo");
        salary.ChangeAmountFrom(YearMonth.Of(2026, 12), Money.FromDecimal(4400m));
        salary.ConfirmReceipt(account, YearMonth.Of(2026, 11), new DateOnly(2026, 11, 5), Money.FromDecimal(4250m), _clock.Today);
        await _fixedIncomes.AddAsync(salary);

        var rental = FixedIncome.Create(
            Guid.NewGuid(), account, YearMonth.Of(2026, 11), null, 28, Money.FromDecimal(800m), "Aluguel Fixo");
        await _fixedIncomes.AddAsync(rental);

        var dto = await _handler.ExecuteAsync(new GetMonthlyDashboardQuery(account.Id, YearMonth.Of(2026, 11)));

        // Cash = 500 + 4250 (confirmed salary receipt) = 4750
        Assert.Equal(4750m, dto.Balance.Amount);
        // Monthly income = 4250 (actual) + 800 (expected) = 5050
        Assert.Equal(5050m, dto.CurrentMonthFlow.MonthlyIncome);
        Assert.Equal(2, dto.CurrentMonthFlow.IncomeEntries!.Count);
        Assert.Contains(dto.CurrentMonthFlow.IncomeEntries, e => e.Description == "Salário Fixo" && e.Amount == 4250m && e.IsReceived);
        Assert.Contains(dto.CurrentMonthFlow.IncomeEntries, e => e.Description == "Aluguel Fixo" && e.Amount == 800m && !e.IsReceived);
        // Dec estimate = 4400 + 800 = 5200; Opening = 4750 + 800 = 5550; Ending = 5550 + 5200 = 10750
        Assert.Equal(5200m, dto.NextMonthEstimate!.EstimatedIncome);
        Assert.Equal(5550m, dto.NextMonthEstimate.ProjectedOpeningBalance);
        Assert.Equal(10750m, dto.NextMonthEstimate.ProjectedEndingBalance);
    }

    [Fact]
    public void DependencyInjection_RegistersGetMonthlyDashboardHandler()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        Assert.Contains(services, d => d.ServiceType == typeof(GetMonthlyDashboardHandler));
    }
}
