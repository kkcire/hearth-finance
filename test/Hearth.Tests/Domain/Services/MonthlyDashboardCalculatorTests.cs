using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.Services;
using Hearth.Domain.ValueObjects;

namespace Hearth.Tests.Domain.Services;

public sealed class MonthlyDashboardCalculatorTests
{
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AccountId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CardId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly CardBillingRule Rule = CardBillingRule.Of(2, 14);

    [Fact]
    public void Calculate_BalanceUsesStrictCashRuleOnCutoffDateAndIgnoresForecasts()
    {
        var today = new DateOnly(2026, 11, 24);
        var account = CreateAccount(initialBalance: 1000m, initialDate: new DateOnly(2026, 10, 1));
        var card = CreditCard.Create(CardId, AccountId, Rule, "Cartão BB", Money.FromDecimal(5000m));

        // Realized income before cutoff (+500) + unreceived expected income (+2000, must be ignored in SALDO)
        var realizedIncome = Income.Create(Guid.NewGuid(), AccountId, new DateOnly(2026, 11, 5), Money.FromDecimal(500m), "Freela");
        realizedIncome.MarkAsReceived(new DateOnly(2026, 11, 5), today);
        var expectedIncome = Income.Create(Guid.NewGuid(), AccountId, new DateOnly(2026, 11, 28), Money.FromDecimal(2000m), "Salário previsto");

        // Paid direct before cutoff (-200) + future direct after cutoff (-150, must be ignored)
        var pastDirect = DirectExpense.Create(Guid.NewGuid(), AccountId, new DateOnly(2026, 11, 10), Money.FromDecimal(200m), "Mercado", SystemCategories.FoodId);
        var futureDirect = DirectExpense.Create(Guid.NewGuid(), AccountId, new DateOnly(2026, 11, 29), Money.FromDecimal(150m), "Agendado", SystemCategories.FoodId);

        // Fixed expense paid before cutoff (-100) + unpaid fixed (-300, must be ignored)
        var paidFixed = FixedExpense.Create(Guid.NewGuid(), AccountId, YearMonth.Of(2026, 11), null, Money.FromDecimal(100m), "Internet", SystemCategories.ServicesId);
        paidFixed.MarkAsPaid(YearMonth.Of(2026, 11), new DateOnly(2026, 11, 12), today);
        var unpaidFixed = FixedExpense.Create(Guid.NewGuid(), AccountId, YearMonth.Of(2026, 11), null, Money.FromDecimal(300m), "Energia", SystemCategories.ServicesId);

        // Card purchase: installment 1 due Nov 14 (<= Nov 24 -> paid -120); installment 2 due Dec 14 (> Nov 24 -> unpaid, ignored)
        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, CardId, new DateOnly(2026, 10, 20), Money.FromDecimal(240m), 2, 1, Rule,
            SystemPurchaseConditions.OwnId, "Fone", SystemCategories.ShoppingId);

        var snapshot = MonthlyDashboardCalculator.Calculate(
            account,
            YearMonth.Of(2026, 11),
            asOfDate: today,
            today: today,
            activeCard: card,
            incomes: [realizedIncome, expectedIncome],
            directExpenses: [pastDirect, futureDirect],
            fixedExpenses: [paidFixed, unpaidFixed],
            purchases: [purchase]);

        // SALDO = 1000 + 500 - 200 - 100 - 120 = 1080.00
        Assert.Equal(Money.FromDecimal(1080m), snapshot.Balance.Balance);
        Assert.Equal(today, snapshot.Balance.Until);
    }

    [Fact]
    public void Calculate_EvaluatesEachMonthInIsolationWithoutCascadingFutureSurpluses()
    {
        var today = new DateOnly(2026, 11, 24);
        var account = CreateAccount(initialBalance: 500m, initialDate: new DateOnly(2026, 10, 1));
        var card = CreditCard.Create(CardId, AccountId, Rule, "Cartão", Money.FromDecimal(5000m));

        // Huge unclosed surplus in Nov 2026: +5000 income, -500 fixed => +4500 in Nov
        var novIncome = Income.Create(Guid.NewGuid(), AccountId, new DateOnly(2026, 11, 28), Money.FromDecimal(5000m), "Bônus Nov");
        var fixedExpense = FixedExpense.Create(Guid.NewGuid(), AccountId, YearMonth.Of(2026, 11), null, Money.FromDecimal(500m), "Internet", SystemCategories.ServicesId);

        // Dec 2026 isolated: +2000 income, -500 fixed, -200 scheduled direct => +1300 (MUST NOT cascade +4500 from Nov!)
        var decIncome = Income.Create(Guid.NewGuid(), AccountId, new DateOnly(2026, 12, 10), Money.FromDecimal(2000m), "Salário Dez");
        var decDirect = DirectExpense.Create(Guid.NewGuid(), AccountId, new DateOnly(2026, 12, 12), Money.FromDecimal(200m), "Viagem", SystemCategories.TransportId);

        var novSnapshot = MonthlyDashboardCalculator.Calculate(
            account, YearMonth.Of(2026, 11), today, today, card,
            [novIncome, decIncome], [decDirect], [fixedExpense], []);

        // Current month (Nov) isolated: 5000 - 500 = 4500 (does NOT add initialBalance 500)
        Assert.Equal(Money.FromDecimal(4500m), novSnapshot.CurrentMonthFlow.MonthlyResult);

        // Next month estimate (Dec) strictly isolated to M+1: 2000 - (500 fixed + 200 scheduled direct) = 1300, SavingsRate = 65.0%
        Assert.NotNull(novSnapshot.NextMonthEstimate);
        Assert.Equal(YearMonth.Of(2026, 12), novSnapshot.NextMonthEstimate!.NextYearMonth);
        Assert.Equal(Money.FromDecimal(2000m), novSnapshot.NextMonthEstimate.EstimatedIncome);
        Assert.Equal(Money.FromDecimal(500m), novSnapshot.NextMonthEstimate.EstimatedFixedExpenses);
        Assert.Equal(Money.FromDecimal(0m), novSnapshot.NextMonthEstimate.EstimatedCardInstallments);
        Assert.Equal(Money.FromDecimal(200m), novSnapshot.NextMonthEstimate.EstimatedDirectExpenses);
        Assert.Equal(Money.FromDecimal(700m), novSnapshot.NextMonthEstimate.EstimatedCommitments);
        Assert.Equal(Money.FromDecimal(1300m), novSnapshot.NextMonthEstimate.ProjectedResult);
        Assert.Equal(65.0m, novSnapshot.NextMonthEstimate.EstimatedSavingsRatePercentage);
    }

    [Theory]
    [InlineData(0, 0, BreakEvenStatus.NoActivity)]              // Empty month -> NoActivity (never orange warning!)
    [InlineData(3000, 1000, BreakEvenStatus.Healthy)]           // Result +2000 > 10% of 3000 (300) -> Healthy
    [InlineData(1000, 899, BreakEvenStatus.Healthy)]            // Result +101 > 10% of 1000 (100) -> Healthy
    [InlineData(1000, 900, BreakEvenStatus.BreakEvenWarning)]   // Result +100 == 10% of 1000 (100) -> Warning
    [InlineData(1000, 950, BreakEvenStatus.BreakEvenWarning)]   // Result +50 <= 10% of 1000 (100) -> Warning
    [InlineData(1000, 1000, BreakEvenStatus.BreakEvenWarning)]  // Result 0 == break-even -> Warning
    [InlineData(900, 1000, BreakEvenStatus.Deficit)]            // Result -100 < 0 -> Deficit
    [InlineData(0, 100, BreakEvenStatus.Deficit)]               // Expense with zero income -> Deficit
    [InlineData(500, 0, BreakEvenStatus.Healthy)]               // Income with zero expenses -> Healthy
    public void ClassifyBreakEven_ClassifiesNoActivityHealthyWarningAndDeficitZones(
        decimal income,
        decimal expenses,
        BreakEvenStatus expected)
    {
        var status = MonthlyDashboardCalculator.ClassifyBreakEven(
            Money.FromDecimal(income),
            Money.FromDecimal(expenses));

        Assert.Equal(expected, status);
    }

    [Fact]
    public void ClassifyBreakEven_SmallFixedExpenseNeverShrinksWarningZone()
    {
        var today = new DateOnly(2026, 11, 24);
        var account = CreateAccount(initialBalance: 0m, initialDate: new DateOnly(2026, 10, 1));

        // Income = 1080, Direct = 950, Fixed = 50 -> TotalExpenses = 1000, Result = +80 (7.4% <= 10% of 1080) -> BreakEvenWarning!
        var income = Income.Create(Guid.NewGuid(), AccountId, new DateOnly(2026, 11, 5), Money.FromDecimal(1080m), "Salário");
        var direct = DirectExpense.Create(Guid.NewGuid(), AccountId, new DateOnly(2026, 11, 10), Money.FromDecimal(950m), "Aluguel", SystemCategories.PersonalId);
        var smallFixed = FixedExpense.Create(Guid.NewGuid(), AccountId, YearMonth.Of(2026, 11), null, Money.FromDecimal(50m), "Streaming", SystemCategories.ServicesId);

        var snapshot = MonthlyDashboardCalculator.Calculate(
            account, YearMonth.Of(2026, 11), today, today, activeCard: null,
            [income], [direct], [smallFixed], []);

        Assert.Equal(BreakEvenStatus.BreakEvenWarning, snapshot.CurrentMonthFlow.BreakEvenStatus);
    }

    [Fact]
    public void Calculate_WhenDueDayLessOrEqualClosingDay_NeverDropsClosedUnpaidInvoiceBetweenClosingAndMonthEnd()
    {
        // Card closes day 25, due day 5. Today is May 28 -> May 5 is Paid, June 5 is CLOSED & UNPAID, July 5 is Open.
        var rule25_5 = CardBillingRule.Of(25, 5);
        var today = new DateOnly(2026, 5, 28);
        var account = CreateAccount(initialBalance: 5000m, initialDate: new DateOnly(2026, 5, 1));
        var card = CreditCard.Create(CardId, AccountId, rule25_5, "Cartão 25/5", Money.FromDecimal(10000m));

        // Purchase A on May 10 -> closes May 25, due June 5 (Closed & Unpaid: R$ 2000)
        var closedPurchase = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, CardId, new DateOnly(2026, 5, 10), Money.FromDecimal(2000m), 1, 1, rule25_5,
            SystemPurchaseConditions.OwnId, "Fatura fechada", SystemCategories.ShoppingId);

        // Purchase B on May 26 -> closes June 25, due July 5 (Open M+2: R$ 500)
        var openPurchase = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, CardId, new DateOnly(2026, 5, 26), Money.FromDecimal(500m), 1, 1, rule25_5,
            SystemPurchaseConditions.OwnId, "Fatura seguinte", SystemCategories.FoodId);

        var snapshot = MonthlyDashboardCalculator.Calculate(
            account, YearMonth.Of(2026, 5), today, today, card,
            [], [], [], [closedPurchase, openPurchase]);

        // MUST surface closed unpaid June 5 invoice (R$ 2000) in CycleAmount, and July 5 (R$ 500) in FutureInstallmentsTotal!
        Assert.Equal(YearMonth.Of(2026, 6), snapshot.CurrentInvoice.BillingMonth);
        Assert.Equal(Money.FromDecimal(2000m), snapshot.CurrentInvoice.CycleAmount);
        Assert.Equal(new DateOnly(2026, 6, 5), snapshot.CurrentInvoice.DueDate);
        Assert.Equal(new DateOnly(2026, 5, 25), snapshot.CurrentInvoice.ClosingDate);
        Assert.Equal(InvoiceStatus.Closed, snapshot.CurrentInvoice.Status);
        Assert.Equal(Money.FromDecimal(500m), snapshot.CurrentInvoice.FutureInstallmentsTotal);
    }

    [Fact]
    public void PurchaseBeforeTrackingStart_WithInvoiceDueOnOrAfterTrackingStart_IsIncludedInDashboardAndCategorySpendingByDueMonth()
    {
        var rule25_5 = CardBillingRule.Of(25, 5);
        var today = new DateOnly(2026, 5, 10);
        var account = CreateAccount(initialBalance: 3000m, initialDate: new DateOnly(2026, 5, 1));
        var card = CreditCard.Create(CardId, AccountId, rule25_5, "Cartão 25/5", Money.FromDecimal(8000m));

        // Purchased Apr 10 -> closes Apr 25, due May 5 (>= TrackingStartDate 2026-05-01!)
        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, CardId, new DateOnly(2026, 4, 10), Money.FromDecimal(1000m), 1, 1, rule25_5,
            SystemPurchaseConditions.OwnId, "Passagem", SystemCategories.TransportId);

        var snapshot = MonthlyDashboardCalculator.Calculate(
            account, YearMonth.Of(2026, 5), today, today, card,
            [], [], [], [purchase]);

        Assert.Equal(Money.FromDecimal(1000m), snapshot.CurrentMonthFlow.CardInvoice);
        Assert.Equal(Money.FromDecimal(2000m), snapshot.Balance.Balance);

        var categorySpending = MonthlyCategorySpendingCalculator.Calculate(
            account, YearMonth.Of(2026, 5), [], [], [], [purchase]);
        var item = Assert.Single(categorySpending.Items);
        Assert.Equal(SystemCategories.TransportId, item.CategoryId);
        Assert.Equal(Money.FromDecimal(1000m), item.AmountSpent);
    }

    [Fact]
    public void Calculate_ClampsExtremeHistoryMonthsWithoutOom_AndValidatesCardAndCutoffDate()
    {
        var today = new DateOnly(2026, 11, 25);
        var account = CreateAccount(initialBalance: 500m, initialDate: new DateOnly(2026, 10, 1));

        // int.MaxValue historyMonths must NOT throw OutOfMemoryException
        var snapshot = MonthlyDashboardCalculator.Calculate(
            account, YearMonth.Of(2026, 11), today, today, activeCard: null,
            [], [], [], [], historyMonths: int.MaxValue);
        Assert.Equal(2, snapshot.MonthlyResultHistory.Count);

        // Future asOfDate rejected
        Assert.Throws<Hearth.Domain.Exceptions.DomainException>(() => MonthlyDashboardCalculator.Calculate(
            account, YearMonth.Of(2026, 11), today.AddDays(1), today, activeCard: null,
            [], [], [], []));

        // Foreign account card rejected
        var foreignCard = CreditCard.Create(Guid.NewGuid(), Guid.NewGuid(), Rule, "Outro", Money.FromDecimal(1000m));
        Assert.Throws<Hearth.Domain.Exceptions.DomainException>(() => MonthlyDashboardCalculator.Calculate(
            account, YearMonth.Of(2026, 11), today, today, foreignCard,
            [], [], [], []));

        // Archived card rejected
        var archivedCard = CreditCard.Create(CardId, AccountId, Rule, "Arquivado", Money.FromDecimal(1000m));
        archivedCard.Archive();
        Assert.Throws<Hearth.Domain.Exceptions.DomainException>(() => MonthlyDashboardCalculator.Calculate(
            account, YearMonth.Of(2026, 11), today, today, archivedCard,
            [], [], [], []));
    }

    [Fact]
    public void Calculate_ClampsHistoryWindowToAccountInitialBalanceDate()
    {
        var today = new DateOnly(2026, 11, 25);
        var account = CreateAccount(initialBalance: 500m, initialDate: new DateOnly(2026, 10, 15));

        var snapshot = MonthlyDashboardCalculator.Calculate(
            account, YearMonth.Of(2026, 11), today, today, activeCard: null,
            [], [], [], [], historyMonths: 6);

        // Must emit ONLY Oct and Nov 2026 — never 4 pre-account months before Oct 2026
        Assert.Equal(2, snapshot.MonthlyResultHistory.Count);
        Assert.Equal(YearMonth.Of(2026, 10), snapshot.MonthlyResultHistory[0].Month);
        Assert.Equal(BreakEvenStatus.NoActivity, snapshot.MonthlyResultHistory[0].BreakEvenStatus);
        Assert.Equal(YearMonth.Of(2026, 11), snapshot.MonthlyResultHistory[1].Month);
        Assert.Equal(BreakEvenStatus.NoActivity, snapshot.MonthlyResultHistory[1].BreakEvenStatus);
    }

    [Fact]
    public void Calculate_WhenViewingCurrentMonthWithPaidInvoice_ResolvesActiveOpenBillingCycle()
    {
        // Today is Nov 25, rule is closing 2 / due 14 -> Nov 14 cycle is already Paid!
        // Open billing cycle for purchases on Nov 25 is closing Dec 2 / due Dec 14.
        var today = new DateOnly(2026, 11, 25);
        var account = CreateAccount(initialBalance: 1000m, initialDate: new DateOnly(2026, 10, 1));
        var card = CreditCard.Create(CardId, AccountId, Rule, "Cartão BB", Money.FromDecimal(5000m));

        // Purchase on Nov 10 -> 3x 100 due Dec 14, Jan 14, Feb 14
        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, CardId, new DateOnly(2026, 11, 10), Money.FromDecimal(300m), 3, 1, Rule,
            SystemPurchaseConditions.OwnId, "Monitor", SystemCategories.ShoppingId);

        var novSnapshot = MonthlyDashboardCalculator.Calculate(
            account, YearMonth.Of(2026, 11), today, today, card,
            [], [], [], [purchase]);

        // Fatura Atual resolves to the open Dec 14 cycle (R$ 100,00) + R$ 200,00 future (Jan+Feb)
        Assert.Equal(YearMonth.Of(2026, 12), novSnapshot.CurrentInvoice.BillingMonth);
        Assert.Equal(Money.FromDecimal(100m), novSnapshot.CurrentInvoice.CycleAmount);
        Assert.Equal(new DateOnly(2026, 12, 14), novSnapshot.CurrentInvoice.DueDate);
        Assert.Equal(new DateOnly(2026, 12, 2), novSnapshot.CurrentInvoice.ClosingDate);
        Assert.Equal(InvoiceStatus.Open, novSnapshot.CurrentInvoice.Status);
        Assert.Equal(Money.FromDecimal(200m), novSnapshot.CurrentInvoice.FutureInstallmentsTotal);

        // When viewing a non-current month (e.g. Oct 2026), it stays anchored on selectedMonth (Oct 2026)
        var octSnapshot = MonthlyDashboardCalculator.Calculate(
            account, YearMonth.Of(2026, 10), today, today, card,
            [], [], [], [purchase]);
        Assert.Equal(YearMonth.Of(2026, 10), octSnapshot.CurrentInvoice.BillingMonth);
        Assert.Equal(InvoiceStatus.Paid, octSnapshot.CurrentInvoice.Status);
    }

    [Fact]
    public void Calculate_ExcludesCreditCardInstallmentsDueBeforeTrackingStartDate_AndKeepsDefinedBalanceOutOfCompetence()
    {
        var today = new DateOnly(2026, 11, 25);
        // Account created Nov 15, 2026 -> TrackingStartDate = 2026-11-01 (Day 1 invariant)
        var account = CreateAccount(initialBalance: 1000m, initialDate: new DateOnly(2026, 11, 15));
        // Update single Day-1 opening balance = R$ 2.300,00 (always anchored on Nov 1!)
        account.SetBalance(Guid.NewGuid(), Money.FromDecimal(2300m), "Extrato dia 1", TestTime.UtcOn(2026, 11, 20));

        var card = CreditCard.Create(CardId, AccountId, Rule, "Cartão BB", Money.FromDecimal(5000m));

        // Purchased Sep 10 -> 2x 150: installment 1 due Oct 14 (< Nov 1 -> excluded!), installment 2 due Nov 14 (>= Nov 1 -> included!)
        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, CardId, new DateOnly(2026, 9, 10), Money.FromDecimal(300m), 2, 1, Rule,
            SystemPurchaseConditions.OwnId, "Jaqueta", SystemCategories.ShoppingId);

        var novIncome = Income.Create(Guid.NewGuid(), account, new DateOnly(2026, 11, 5), Money.FromDecimal(1000m), "Salário");
        novIncome.MarkAsReceived(account, new DateOnly(2026, 11, 5), today);

        var novSnapshot = MonthlyDashboardCalculator.Calculate(
            account, YearMonth.Of(2026, 11), today, today, card,
            [novIncome], [], [], [purchase]);

        // SALDO = Day-1 InitialBalance (2300) + Nov 5 income (1000) - Nov 14 installment (150) = 3150
        Assert.Equal(Money.FromDecimal(2300m), novSnapshot.Balance.InitialBalance);
        Assert.Equal(Money.FromDecimal(1000m), novSnapshot.Balance.Incomes);
        Assert.Equal(Money.FromDecimal(150m), novSnapshot.Balance.CardInvoices);
        Assert.Equal(Money.FromDecimal(3150m), novSnapshot.Balance.Balance);

        // Competence metrics strictly EXCLUDE OpeningBalance and DefinedBalance:
        // Income = 1000, Expenses = 150 (Nov 14 only; Oct 14 excluded), Result = 850, SavingsRate = 85.0%
        Assert.Equal(Money.FromDecimal(1000m), novSnapshot.CurrentMonthFlow.MonthlyIncome);
        Assert.Equal(Money.FromDecimal(150m), novSnapshot.CurrentMonthFlow.CardInvoice);
        Assert.Equal(Money.FromDecimal(850m), novSnapshot.CurrentMonthFlow.MonthlyResult);
        Assert.Equal(85.0m, novSnapshot.CurrentMonthFlow.SavingsRatePercentage);

        // Chart history never emits months prior to TrackingStartPeriod (2026-11)
        var historyPoint = Assert.Single(novSnapshot.MonthlyResultHistory);
        Assert.Equal(YearMonth.Of(2026, 11), historyPoint.Month);

        // Querying a month prior to TrackingStartPeriod yields empty history and zero flow
        var preStartSnapshot = MonthlyDashboardCalculator.Calculate(
            account, YearMonth.Of(2026, 10), today, today, card,
            [novIncome], [], [], [purchase]);
        Assert.Empty(preStartSnapshot.MonthlyResultHistory);
        Assert.Equal(Money.Zero, preStartSnapshot.CurrentMonthFlow.CardInvoice);
    }

    [Fact]
    public void Calculate_HandlesYearMonthBoundariesGracefully()
    {
        var minDate = new DateOnly(YearMonth.MinYear, 1, 1);
        var minAccount = CreateAccount(initialBalance: 0m, initialDate: minDate);

        var minSnapshot = MonthlyDashboardCalculator.Calculate(
            minAccount, YearMonth.Of(YearMonth.MinYear, 1), minDate, minDate, activeCard: null,
            [], [], [], [], historyMonths: 6);
        Assert.Single(minSnapshot.MonthlyResultHistory);

        var maxDate = new DateOnly(YearMonth.MaxYear, 12, 15);
        var maxAccount = CreateAccount(initialBalance: 500m, initialDate: new DateOnly(YearMonth.MaxYear, 12, 1));

        var maxSnapshot = MonthlyDashboardCalculator.Calculate(
            maxAccount, YearMonth.Of(YearMonth.MaxYear, 12), maxDate, maxDate, activeCard: null,
            [], [], [], [], historyMonths: 6);
        Assert.Null(maxSnapshot.NextMonthEstimate);
    }

    [Theory]
    [InlineData(0, 500, 0)]
    [InlineData(0, 0, 0)]
    [InlineData(2000, 2500, 0)]
    [InlineData(2275, 1036.98, 54.4)]
    [InlineData(4000, 1000, 75.0)]
    public void CalculateSavingsRate_HandlesPositiveDeficitAndZeroIncomeEdgeCases(
        decimal income,
        decimal expenses,
        decimal expectedRate)
    {
        var rate = MonthlyDashboardCalculator.CalculateSavingsRate(
            Money.FromDecimal(income),
            Money.FromDecimal(expenses));

        Assert.Equal(expectedRate, rate);
    }

    [Fact]
    public void Calculate_AggregatesCurrentInvoiceAndFutureInstallmentsRespectingConditionFilter()
    {
        var today = new DateOnly(2026, 11, 24);
        var account = CreateAccount(initialBalance: 1000m, initialDate: new DateOnly(2026, 10, 1));
        var card = CreditCard.Create(CardId, AccountId, Rule, "Cartão BB", Money.FromDecimal(8000m));

        // Own purchase on Nov 10 (rule 2/14 -> closes Dec 2, due Dec 14): 3x of 100 = 300
        // Dec 14 (100) + Jan 14 (100) + Feb 14 (100) => Dec cycle = 100, future after Dec = 200
        var ownPurchase = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, CardId, new DateOnly(2026, 11, 10), Money.FromDecimal(300m), 3, 1, Rule,
            SystemPurchaseConditions.OwnId, "Teclado", SystemCategories.ShoppingId);

        // Others purchase on Nov 12: 2x of 250 = 500 => Dec cycle = 250, future after Dec = 250
        var othersPurchase = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, CardId, new DateOnly(2026, 11, 12), Money.FromDecimal(500m), 2, 1, Rule,
            SystemPurchaseConditions.OthersId, "Presente", SystemCategories.ShoppingId);

        var allSnapshot = MonthlyDashboardCalculator.Calculate(
            account, YearMonth.Of(2026, 12), today, today, card,
            [], [], [], [ownPurchase, othersPurchase], conditionId: null);

        Assert.Equal(Money.FromDecimal(350m), allSnapshot.CurrentInvoice.CycleAmount);
        Assert.Equal(new DateOnly(2026, 12, 14), allSnapshot.CurrentInvoice.DueDate);
        Assert.Equal(Money.FromDecimal(450m), allSnapshot.CurrentInvoice.FutureInstallmentsTotal);

        var ownOnlySnapshot = MonthlyDashboardCalculator.Calculate(
            account, YearMonth.Of(2026, 12), today, today, card,
            [], [], [], [ownPurchase, othersPurchase], conditionId: SystemPurchaseConditions.OwnId);

        Assert.Equal(Money.FromDecimal(100m), ownOnlySnapshot.CurrentInvoice.CycleAmount);
        Assert.Equal(Money.FromDecimal(200m), ownOnlySnapshot.CurrentInvoice.FutureInstallmentsTotal);
        Assert.Equal(Money.FromDecimal(100m), ownOnlySnapshot.NextMonthEstimate!.EstimatedCardInstallments);
    }

    [Fact]
    public void Calculate_OngoingPurchasesWithInitialInstallmentSkippingEntryMonth_StillCountEntryMonthPaidInvoiceAcrossAllCalculations()
    {
        // Account created Nov 25, 2026 -> TrackingStartDate = 2026-11-01 (Day 1 invariant).
        // Card closes day 2, due day 14 -> Nov 14, 2026 invoice is already Paid on Nov 25.
        var today = new DateOnly(2026, 11, 25);
        var account = CreateAccount(initialBalance: 1000m, initialDate: today);
        var card = CreditCard.Create(CardId, AccountId, Rule, "Cartão BB", Money.FromDecimal(5000m));

        // 1) 10x from Mar 2, 2026 with InitialInstallment = 9 (inst 1..7 Apr..Oct < Nov 1; inst 8 Nov 14 = 240; inst 9 Dec 14 = 240; inst 10 Jan 14 2027 = 240)
        var marchPurchase = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, CardId, new DateOnly(2026, 3, 2), Money.FromDecimal(2400m), 10, 9, Rule,
            SystemPurchaseConditions.OwnId, "Celular", SystemCategories.ShoppingId);

        // 2) 10x from Jun 12, 2026 with InitialInstallment = 6 (inst 1..4 Jul..Oct < Nov 1; inst 5 Nov 14 = 74.82 .. inst 10 Apr 14 2027 = 74.82)
        var juneToApril2027Purchase = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, CardId, new DateOnly(2026, 6, 12), Money.FromDecimal(748.25m), 10, 6, Rule,
            SystemPurchaseConditions.OwnId, "Mercado Livre", SystemCategories.ShoppingId);

        var novIncome = Income.Create(Guid.NewGuid(), account, new DateOnly(2026, 11, 5), Money.FromDecimal(2000m), "Salário");
        novIncome.MarkAsReceived(account, new DateOnly(2026, 11, 5), today);

        CreditCardPurchase[] purchases = [marchPurchase, juneToApril2027Purchase];

        var novSnapshot = MonthlyDashboardCalculator.Calculate(
            account, YearMonth.Of(2026, 11), today, today, card,
            [novIncome], [], [], purchases);

        // Entry-month (Nov 2026) paid invoice MUST sum inst 8 (240.00) + inst 5 (74.82) = 314.82 across all metrics
        var expectedNovInvoice = Money.FromDecimal(314.82m);
        Assert.Equal(expectedNovInvoice, novSnapshot.CurrentMonthFlow.CardInvoice);
        Assert.Equal(expectedNovInvoice, novSnapshot.CurrentMonthFlow.TotalExpenses);
        Assert.Equal(Money.FromDecimal(1685.18m), novSnapshot.CurrentMonthFlow.MonthlyResult);
        Assert.Equal(84.3m, novSnapshot.CurrentMonthFlow.SavingsRatePercentage);
        Assert.Equal(expectedNovInvoice, novSnapshot.Balance.CardInvoices);
        Assert.Equal(Money.FromDecimal(2685.18m), novSnapshot.Balance.Balance);

        // History point for Nov 2026 matches
        var novHistory = Assert.Single(novSnapshot.MonthlyResultHistory);
        Assert.Equal(expectedNovInvoice, novHistory.CardInvoice);

        // Category spending in Nov 2026 matches
        var spending = MonthlyCategorySpendingCalculator.Calculate(account, YearMonth.Of(2026, 11), [], [], [], purchases);
        var shopping = Assert.Single(spending.Items);
        Assert.Equal(expectedNovInvoice, shopping.AmountSpent);
        Assert.Equal(2, shopping.Entries.Count);

        // Pre-tracking Oct 2026 stays strictly R$ 0.00, while future Mar/Apr 2027 keep counting normally
        Assert.Equal(Money.Zero, InvoiceCalculator.CalculateByDueMonth(AccountId, YearMonth.Of(2026, 10), purchases, minDueDate: account.TrackingStartDate));
        Assert.Equal(Money.FromDecimal(74.82m), InvoiceCalculator.CalculateByDueMonth(AccountId, YearMonth.Of(2027, 3), purchases, minDueDate: account.TrackingStartDate));
        Assert.Equal(Money.FromDecimal(74.82m), InvoiceCalculator.CalculateByDueMonth(AccountId, YearMonth.Of(2027, 4), purchases, minDueDate: account.TrackingStartDate));

        // Credit limit excludes already-paid Nov 14 installments and counts only unpaid future cycles (Dec..Apr 2027):
        // marchPurchase: 2 * 240 = 480; juneToApril2027Purchase: 5 * 74.82 = 374.10 => total 854.10
        Assert.Equal(Money.FromDecimal(854.10m), CreditCardLimitCalculator.CalculateUsedLimit(CardId, today, purchases, account.TrackingStartDate));
    }

    [Fact]
    public void Calculate_Option1SavingsRate_UsesOwnMonthlyIncomeDenominatorAcrossTodasAndPropriaWithoutThirdPartyDilution()
    {
        var today = new DateOnly(2026, 11, 24);
        var account = CreateAccount(initialBalance: 0m, initialDate: new DateOnly(2026, 10, 1));
        var card = CreditCard.Create(CardId, AccountId, Rule, "Cartão", Money.FromDecimal(5000m));
        var paiId = Guid.NewGuid();

        // R$ 2.000 own income + R$ 950 third-party reimbursement (Pai)
        var ownIncome = Income.Create(Guid.NewGuid(), AccountId, new DateOnly(2026, 11, 5), Money.FromDecimal(2000m), "Salário", SystemPurchaseConditions.OwnId);
        ownIncome.MarkAsReceived(new DateOnly(2026, 11, 5), today);
        var paiReimbursement = Income.Create(Guid.NewGuid(), AccountId, new DateOnly(2026, 11, 10), Money.FromDecimal(950m), "Reembolso Pai", paiId);
        paiReimbursement.MarkAsReceived(new DateOnly(2026, 11, 10), today);

        // R$ 300 own direct expense + R$ 950 Pai card expense due Nov 14 (paid on Nov 24)
        var ownExpense = DirectExpense.Create(Guid.NewGuid(), AccountId, new DateOnly(2026, 11, 8), Money.FromDecimal(300m), "Mercado", SystemCategories.FoodId);
        var paiCardExpense = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, CardId, new DateOnly(2026, 10, 15), Money.FromDecimal(950m), 1, 1, Rule,
            paiId, "Compra Pai", SystemCategories.ShoppingId);

        // Under "Todas" (conditionId = null):
        // Receita = 2950, OwnMonthlyIncome = 2000, Despesa = 1250, Resultado = 1700, Taxa = (1700 / 2000) * 100 = 85.0% (NOT 57.6%)
        var allSnapshot = MonthlyDashboardCalculator.Calculate(
            account, YearMonth.Of(2026, 11), today, today, card,
            [ownIncome, paiReimbursement], [ownExpense], [], [paiCardExpense], conditionId: null);

        Assert.Equal(Money.FromDecimal(2950m), allSnapshot.CurrentMonthFlow.MonthlyIncome);
        Assert.Equal(Money.FromDecimal(2000m), allSnapshot.CurrentMonthFlow.OwnMonthlyIncome);
        Assert.Equal(Money.FromDecimal(1250m), allSnapshot.CurrentMonthFlow.TotalExpenses);
        Assert.Equal(Money.FromDecimal(1700m), allSnapshot.CurrentMonthFlow.MonthlyResult);
        Assert.Equal(85.0m, allSnapshot.CurrentMonthFlow.SavingsRatePercentage);
        Assert.Equal(Money.FromDecimal(1700m), allSnapshot.Balance.Balance);

        // Under "Própria" (conditionId = OwnId):
        // Receita = 2000, OwnMonthlyIncome = 2000, Despesa = 300, Resultado = 1700, Taxa = 85.0% (no phantom +2650 surplus!)
        var ownSnapshot = MonthlyDashboardCalculator.Calculate(
            account, YearMonth.Of(2026, 11), today, today, card,
            [ownIncome, paiReimbursement], [ownExpense], [], [paiCardExpense], conditionId: SystemPurchaseConditions.OwnId);

        Assert.Equal(Money.FromDecimal(2000m), ownSnapshot.CurrentMonthFlow.MonthlyIncome);
        Assert.Equal(Money.FromDecimal(2000m), ownSnapshot.CurrentMonthFlow.OwnMonthlyIncome);
        Assert.Equal(Money.FromDecimal(300m), ownSnapshot.CurrentMonthFlow.TotalExpenses);
        Assert.Equal(Money.FromDecimal(1700m), ownSnapshot.CurrentMonthFlow.MonthlyResult);
        Assert.Equal(85.0m, ownSnapshot.CurrentMonthFlow.SavingsRatePercentage);
        Assert.Equal(Money.FromDecimal(1700m), ownSnapshot.Balance.Balance);

        // Under "Pai" (conditionId = paiId): OwnMonthlyIncome == 0 -> SavingsRatePercentage == 0m
        var paiSnapshot = MonthlyDashboardCalculator.Calculate(
            account, YearMonth.Of(2026, 11), today, today, card,
            [ownIncome, paiReimbursement], [ownExpense], [], [paiCardExpense], conditionId: paiId);

        Assert.Equal(Money.FromDecimal(950m), paiSnapshot.CurrentMonthFlow.MonthlyIncome);
        Assert.Equal(Money.Zero, paiSnapshot.CurrentMonthFlow.OwnMonthlyIncome);
        Assert.Equal(0m, paiSnapshot.CurrentMonthFlow.SavingsRatePercentage);
    }

    [Fact]
    public void CalculateSavingsRate_WhenOwnIncomeIsZeroOrReimbursementExceedsOwnIncome_ReturnsZeroOrCapsAt100()
    {
        // Zero own income even with third-party reimbursement -> 0%
        Assert.Equal(0m, MonthlyDashboardCalculator.CalculateSavingsRate(
            monthlyIncome: Money.FromDecimal(950m),
            totalExpenses: Money.FromDecimal(300m),
            ownMonthlyIncome: Money.Zero));

        // Surplus above 100% of own income (e.g. reimbursement arrived before card invoice) caps cleanly at 100%
        Assert.Equal(100m, MonthlyDashboardCalculator.CalculateSavingsRate(
            monthlyIncome: Money.FromDecimal(2950m),
            totalExpenses: Money.FromDecimal(300m),
            ownMonthlyIncome: Money.FromDecimal(2000m)));
    }

    private static Account CreateAccount(decimal initialBalance, DateOnly initialDate) =>
        Account.Create(AccountId, OwnerId, "Conta Principal", Money.FromDecimal(initialBalance), TestTime.UtcOn(initialDate));
}
