using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.Services;
using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.Services;

public class MonthlySurplusCalculatorTests
{
    private static readonly Guid OwnerUserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid OtherAccountId = Guid.NewGuid();
    private static readonly Guid ActiveCardId = Guid.NewGuid();
    private static readonly Guid ArchivedCardId = Guid.NewGuid();
    private static readonly CardBillingRule Rule3_12 = CardBillingRule.Of(3, 12);
    private static readonly CardBillingRule Rule10_20 = CardBillingRule.Of(10, 20);
    private static readonly CardBillingRule Rule25_5 = CardBillingRule.Of(25, 5);

    private static Money M(decimal amount) => Money.FromDecimal(amount);

    private static YearMonth YM(int year, int month) => YearMonth.Of(year, month);

    private static DateOnly Day(int year, int month, int day) => new(year, month, day);

    private static Account NewAccount(
        decimal initialBalance = 1000.00m,
        DateOnly? initialDate = null,
        Guid? id = null) =>
        Account.Create(
            id ?? AccountId,
            OwnerUserId,
            "Conta principal",
            M(initialBalance),
            initialDate ?? Day(2026, 10, 1),
            isInitialBalanceConfirmed: true);

    [Fact]
    public void CalculateSeries_InvalidArguments_Throw()
    {
        var account = NewAccount();
        var month = YM(2026, 11);
        var today = Day(2026, 11, 20);

        Assert.Throws<ArgumentNullException>(() =>
            MonthlySurplusCalculator.CalculateSeries(null!, month, today, [], [], [], []));
        Assert.Throws<ArgumentNullException>(() =>
            MonthlySurplusCalculator.CalculateSeries(account, null!, today, [], [], [], []));
        Assert.Throws<ArgumentNullException>(() =>
            MonthlySurplusCalculator.CalculateSeries(account, month, today, null!, [], [], []));
        Assert.Throws<ArgumentNullException>(() =>
            MonthlySurplusCalculator.CalculateSeries(account, month, today, [], null!, [], []));
        Assert.Throws<ArgumentNullException>(() =>
            MonthlySurplusCalculator.CalculateSeries(account, month, today, [], [], null!, []));
        Assert.Throws<ArgumentNullException>(() =>
            MonthlySurplusCalculator.CalculateSeries(account, month, today, [], [], [], null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MonthlySurplusCalculator.CalculateSeries(account, month, today, [], [], [], [], pastMonths: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MonthlySurplusCalculator.CalculateSeries(account, month, today, [], [], [], [], futureMonths: -1));
    }

    [Fact]
    public void CalculateSeries_CurrentMonth_PreventsDoubleCountingAcrossAllFourSources()
    {
        var account = NewAccount(initialBalance: 1000.00m, initialDate: Day(2026, 10, 1));
        var today = Day(2026, 11, 12);

        // Settled <= today: must live inside StartingCashBalance only (NOT counted again in cascade components)
        var salaryReceived = Income.Create(Guid.NewGuid(), AccountId, Day(2026, 11, 5), M(5000m), "Salário");
        salaryReceived.MarkAsReceived(Day(2026, 11, 5), today);

        var directPaid = DirectExpense.Create(
            Guid.NewGuid(), AccountId, Day(2026, 11, 10), M(200m), "Mercado", SystemCategories.FoodId);

        var rentPaid = FixedExpense.Create(
            Guid.NewGuid(), AccountId, YM(2026, 11), null, M(1200m), "Aluguel", SystemCategories.ServicesId);
        rentPaid.MarkAsPaid(YM(2026, 11), Day(2026, 11, 5), today);

        // DueDate == today (Nov 12) -> auto-settled in SaldoDeCaixa, must NOT appear in CardInvoiceDue
        var cardSettledToday = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, ActiveCardId, Day(2026, 10, 10), M(300m), 1, 1,
            Rule3_12, SystemPurchaseConditions.OwnId, "Fatura hoje", SystemCategories.ShoppingId);

        // Pending in Nov (not yet in SaldoDeCaixa): MUST appear in cascade components
        var freelanceExpected = Income.Create(Guid.NewGuid(), AccountId, Day(2026, 11, 25), M(800m), "Freela");
        var overdueBonus = Income.Create(Guid.NewGuid(), AccountId, Day(2026, 11, 3), M(200m), "Bônus atrasado");
        var internetUnpaid = FixedExpense.Create(
            Guid.NewGuid(), AccountId, YM(2026, 11), null, M(100m), "Internet", SystemCategories.ServicesId);
        var scheduledDirect = DirectExpense.Create(
            Guid.NewGuid(), AccountId, Day(2026, 11, 20), M(150m), "Consulta", SystemCategories.HealthId);

        // DueDate == Nov 20 > today -> pending in CardInvoiceDue
        var cardDueLater = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, ActiveCardId, Day(2026, 10, 15), M(450m), 1, 1,
            Rule10_20, SystemPurchaseConditions.OwnId, "Fatura futura", SystemCategories.ShoppingId);

        // Noise from another account and previously-paid installment -> must be ignored
        var otherIncome = Income.Create(Guid.NewGuid(), OtherAccountId, Day(2026, 11, 25), M(9999m), "Outra conta");
        var previouslyPaidCard = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, ActiveCardId, Day(2026, 9, 15), M(600m), 2, 2,
            Rule10_20, SystemPurchaseConditions.OwnId, "Em andamento", SystemCategories.ShoppingId);
        // Installment 1 due Oct 20 (previously paid -> ignored), Installment 2 due Nov 20 (active -> +300!)

        var series = MonthlySurplusCalculator.CalculateSeries(
            account,
            YM(2026, 11),
            today,
            [salaryReceived, freelanceExpected, overdueBonus, otherIncome],
            [directPaid, scheduledDirect],
            [rentPaid, internetUnpaid],
            [cardSettledToday, cardDueLater, previouslyPaidCard],
            pastMonths: 1,
            futureMonths: 0);

        var nov = series.SelectedMonth;
        Assert.Equal(YM(2026, 11), nov.Month);
        Assert.False(nov.IsHistorical);
        Assert.True(nov.IsCurrentMonth);

        // SaldoDeCaixa = 1000 + 5000 - 200 - 1200 - 300 = 4300
        Assert.Equal(M(4300m), nov.StartingCashBalance);
        Assert.Equal(M(1000m), nov.ExpectedIncomes);
        Assert.Equal(M(5300m), nov.TotalAvailable);
        Assert.Equal(M(750m), nov.CardInvoiceDue); // 450 + 300
        Assert.Equal(M(100m), nov.FixedExpenses);
        Assert.Equal(M(150m), nov.DirectExpenses);
        Assert.Equal(M(1000m), nov.TotalDiscounts);
        Assert.Equal(M(4300m), nov.ProjectedSurplus);

        // Entire November net variation = ProjectedSurplus (4300) - OpeningCash (1000) = +3300
        Assert.Equal(M(3300m), nov.NetMonthlyVariation);
    }

    [Fact]
    public void CalculateSeries_SettlingPendingItemsToday_PreservesProjectedSurplusAndNetVariation()
    {
        var account = NewAccount(initialBalance: 2000m, initialDate: Day(2026, 11, 1));
        var today = Day(2026, 11, 15);

        var salary = Income.Create(Guid.NewGuid(), AccountId, Day(2026, 11, 20), M(3000m), "Salário");
        var rent = FixedExpense.Create(
            Guid.NewGuid(), AccountId, YM(2026, 11), null, M(1200m), "Aluguel", SystemCategories.ServicesId);

        var before = MonthlySurplusCalculator.CalculateSeries(
            account, YM(2026, 11), today, [salary], [], [rent], [], pastMonths: 0, futureMonths: 0);

        // User marks both income and rent as settled today: cash changes, but end-of-month surplus must stay identical!
        salary.MarkAsReceived(today, today);
        rent.MarkAsPaid(YM(2026, 11), today, today);

        var after = MonthlySurplusCalculator.CalculateSeries(
            account, YM(2026, 11), today, [salary], [], [rent], [], pastMonths: 0, futureMonths: 0);

        Assert.Equal(M(2000m), before.SelectedMonth.StartingCashBalance);
        Assert.Equal(M(3800m), after.SelectedMonth.StartingCashBalance);
        Assert.Equal(M(3000m), before.SelectedMonth.ExpectedIncomes);
        Assert.Equal(Money.Zero, after.SelectedMonth.ExpectedIncomes);
        Assert.Equal(M(1200m), before.SelectedMonth.FixedExpenses);
        Assert.Equal(Money.Zero, after.SelectedMonth.FixedExpenses);

        Assert.Equal(before.SelectedMonth.ProjectedSurplus, after.SelectedMonth.ProjectedSurplus);
        Assert.Equal(before.SelectedMonth.NetMonthlyVariation, after.SelectedMonth.NetMonthlyVariation);
        Assert.Equal(M(3800m), after.SelectedMonth.ProjectedSurplus);
        Assert.Equal(M(1800m), after.SelectedMonth.NetMonthlyVariation);
    }

    [Fact]
    public void CalculateSeries_FutureMonths_RollOverChainedSurplusIncludeArchivedCardsAndExcludeAnticipatedPayments()
    {
        var account = NewAccount(initialBalance: 2100m, initialDate: Day(2026, 11, 1));
        var today = Day(2026, 11, 20);

        // Rent active from Nov onward; Dec is paid early on Nov 18 (anticipation!)
        var rent = FixedExpense.Create(
            Guid.NewGuid(), AccountId, YM(2026, 11), null, M(500m), "Aluguel", SystemCategories.ServicesId);
        rent.MarkAsPaid(YM(2026, 12), Day(2026, 11, 18), today);

        var internet = FixedExpense.Create(
            Guid.NewGuid(), AccountId, YM(2026, 12), null, M(100m), "Internet", SystemCategories.ServicesId);

        var decSalary = Income.Create(Guid.NewGuid(), AccountId, Day(2026, 12, 5), M(3000m), "Salário Dez");

        // Active card with DueDay < ClosingDay (25/5): purchase on Nov 10 (400 in 2x):
        // Inst 1 closes Nov 25, DueDate Dec 5 (DueMonth 2026-12) -> 200
        // Inst 2 closes Dec 25, DueDate Jan 5 (DueMonth 2027-01) -> 200
        var activeCardPurchase = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, ActiveCardId, Day(2026, 11, 10), M(400m), 2, 1,
            Rule25_5, SystemPurchaseConditions.OwnId, "Notebook", SystemCategories.ShoppingId);

        // Archived card purchase on Nov 10 (300 in 1x, rule 3/12): closes Dec 3, DueDate Dec 12 -> 300
        var archivedCardPurchase = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, ArchivedCardId, Day(2026, 11, 10), M(300m), 1, 1,
            Rule3_12, Guid.NewGuid(), "Cartão arquivado", SystemCategories.ShoppingId);

        var series = MonthlySurplusCalculator.CalculateSeries(
            account,
            YM(2026, 12),
            today,
            [decSalary],
            [],
            [rent, internet],
            [activeCardPurchase, archivedCardPurchase],
            pastMonths: 0,
            futureMonths: 2);

        Assert.Equal(3, series.Points.Count);

        // Nov: Initial 2100 - 500 (Dec rent paid Nov 18) = 1600 cash; remaining Nov rent = 500 -> surplus = 1100
        var nov = series.Points[0];
        Assert.Equal(YM(2026, 11), nov.Month);
        Assert.Equal(M(1600m), nov.StartingCashBalance);
        Assert.Equal(M(500m), nov.FixedExpenses);
        Assert.Equal(M(1100m), nov.ProjectedSurplus);
        Assert.Equal(M(-1000m), nov.NetMonthlyVariation);

        // Dec: starts from Nov surplus (1100) + 3000 salary = 4100;
        // card = 200 (active) + 300 (archived) = 500;
        // fixed = 100 (internet only; Dec rent already paid Nov 18 -> excluded!)
        // surplus = 4100 - 500 - 100 = 3500; net variation = +2400
        var dec = series.Points[1];
        Assert.Equal(dec, series.SelectedMonth);
        Assert.Equal(YM(2026, 12), dec.Month);
        Assert.False(dec.IsHistorical);
        Assert.False(dec.IsCurrentMonth);
        Assert.Equal(M(1100m), dec.StartingCashBalance);
        Assert.Equal(M(3000m), dec.ExpectedIncomes);
        Assert.Equal(M(4100m), dec.TotalAvailable);
        Assert.Equal(M(500m), dec.CardInvoiceDue);
        Assert.Equal(M(100m), dec.FixedExpenses);
        Assert.Equal(M(3500m), dec.ProjectedSurplus);
        Assert.Equal(M(2400m), dec.NetMonthlyVariation);

        // Jan 2027: starts from Dec surplus (3500);
        // card = 200 (2nd inst of Rule 25/5); fixed = 500 (rent) + 100 (internet) = 600;
        // surplus = 3500 - 200 - 600 = 2700; net variation = -800
        var jan = series.Points[2];
        Assert.Equal(YM(2027, 1), jan.Month);
        Assert.Equal(M(3500m), jan.StartingCashBalance);
        Assert.Equal(M(200m), jan.CardInvoiceDue);
        Assert.Equal(M(600m), jan.FixedExpenses);
        Assert.Equal(M(2700m), jan.ProjectedSurplus);
        Assert.Equal(M(-800m), jan.NetMonthlyVariation);
    }

    [Fact]
    public void CalculateSeries_HistoricalMonthsMatchRealBalanceClipBeforeInitialDateAndSupportOutOfWindowSelection()
    {
        var account = NewAccount(initialBalance: 1000m, initialDate: Day(2026, 9, 1));
        var today = Day(2026, 11, 20);

        var sepSalary = Income.Create(Guid.NewGuid(), AccountId, Day(2026, 9, 5), M(2000m), "Salário Set");
        sepSalary.MarkAsReceived(Day(2026, 9, 5), today);

        var octSalary = Income.Create(Guid.NewGuid(), AccountId, Day(2026, 10, 5), M(2500m), "Salário Out");
        octSalary.MarkAsReceived(Day(2026, 10, 5), today);

        var sepDirect = DirectExpense.Create(
            Guid.NewGuid(), AccountId, Day(2026, 9, 15), M(500m), "Mercado", SystemCategories.FoodId);

        var octFixed = FixedExpense.Create(
            Guid.NewGuid(), AccountId, YM(2026, 9), null, M(700m), "Aluguel", SystemCategories.ServicesId);
        octFixed.MarkAsPaid(YM(2026, 10), Day(2026, 10, 10), today);

        var octCard = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, ActiveCardId, Day(2026, 9, 10), M(300m), 1, 1,
            Rule3_12, SystemPurchaseConditions.OwnId, "Tênis", SystemCategories.ShoppingId); // Due Oct 12

        Income[] incomes = [sepSalary, octSalary];
        DirectExpense[] directs = [sepDirect];
        FixedExpense[] fixedExpenses = [octFixed];
        CreditCardPurchase[] purchases = [octCard];

        var series = MonthlySurplusCalculator.CalculateSeries(
            account, YM(2026, 10), today, incomes, directs, fixedExpenses, purchases, pastMonths: 6, futureMonths: 2);

        // Clipped at InitialBalanceDate month (2026-09): Sep, Oct, Nov, Dec, Jan => 5 points
        Assert.Equal(5, series.Points.Count);
        Assert.Equal(YM(2026, 9), series.Points[0].Month);
        Assert.Equal(YM(2027, 1), series.Points[^1].Month);

        var sep = series.Points[0];
        var oct = series.Points[1];
        Assert.True(sep.IsHistorical);
        Assert.True(oct.IsHistorical);
        Assert.Equal(oct, series.SelectedMonth);

        var sepReal = RealBalanceCalculator.Calculate(account, YM(2026, 9).LastDay, incomes, directs, fixedExpenses, purchases);
        var octReal = RealBalanceCalculator.Calculate(account, YM(2026, 10).LastDay, incomes, directs, fixedExpenses, purchases);

        Assert.Equal(sepReal.Balance, sep.ProjectedSurplus);
        Assert.Equal(octReal.Balance, oct.ProjectedSurplus);
        Assert.Equal(M(2500m), sep.ProjectedSurplus); // 1000 + 2000 - 500
        Assert.Equal(M(4000m), oct.ProjectedSurplus); // 2500 + 2500 - 700 - 300

        // Selecting a far future month outside futureMonths window still rolls forward accurately
        var farFuture = MonthlySurplusCalculator.CalculateSeries(
            account, YM(2027, 4), today, incomes, directs, fixedExpenses, purchases, pastMonths: 6, futureMonths: 2);
        Assert.Equal(5, farFuture.Points.Count);
        Assert.Equal(YM(2027, 4), farFuture.SelectedMonth.Month);
        // From Nov to Apr (6 months) each deducts 700 fixed -> 4000 - 6*700 = -200
        Assert.Equal(M(-200m), farFuture.SelectedMonth.ProjectedSurplus);

        // Selecting a month prior to InitialBalanceDate returns initial balance with zero movements
        var preAccount = MonthlySurplusCalculator.CalculateSeries(
            account, YM(2026, 7), today, incomes, directs, fixedExpenses, purchases, pastMonths: 6, futureMonths: 2);
        Assert.True(preAccount.SelectedMonth.IsHistorical);
        Assert.Equal(M(1000m), preAccount.SelectedMonth.ProjectedSurplus);
        Assert.Equal(Money.Zero, preAccount.SelectedMonth.NetMonthlyVariation);
    }

    [Fact]
    public void CalculateSeries_AverageMonthlyVariation_UsesNetMonthlyFlowAndSatisfiesTelescopingInvariant()
    {
        var account = NewAccount(initialBalance: 1000m, initialDate: Day(2026, 10, 1));
        var today = Day(2026, 11, 15);

        // Oct: +500 net -> closing 1500
        var octIncome = Income.Create(Guid.NewGuid(), AccountId, Day(2026, 10, 5), M(500m), "Out");
        octIncome.MarkAsReceived(Day(2026, 10, 5), today);

        // Nov: +1000 net -> closing 2500
        var novIncome = Income.Create(Guid.NewGuid(), AccountId, Day(2026, 11, 25), M(1000m), "Nov");

        // Dec: -300 net -> closing 2200
        var decExpense = FixedExpense.Create(
            Guid.NewGuid(), AccountId, YM(2026, 12), YM(2026, 12), M(300m), "Dez", SystemCategories.ServicesId);

        var series = MonthlySurplusCalculator.CalculateSeries(
            account, YM(2026, 11), today, [octIncome, novIncome], [], [decExpense], [], pastMonths: 1, futureMonths: 1);

        Assert.Equal(3, series.Points.Count);
        Assert.Equal(M(500m), series.Points[0].NetMonthlyVariation);
        Assert.Equal(M(1000m), series.Points[1].NetMonthlyVariation);
        Assert.Equal(M(-300m), series.Points[2].NetMonthlyVariation);

        // Average of net variations = (500 + 1000 - 300) / 3 = +400.00 (NOT average of 1500, 2500, 2200 = 2066.67!)
        Assert.Equal(M(400m), series.AverageMonthlyVariation);

        // Telescoping invariant: InitialBalance + sum(NetMonthlyVariation) == final ProjectedSurplus
        var totalVariation = Money.Sum(series.Points.Select(p => p.NetMonthlyVariation));
        Assert.Equal(series.Points[^1].ProjectedSurplus, account.InitialBalance + totalVariation);
        Assert.Equal(M(2200m), series.Points[^1].ProjectedSurplus);

        // Value equality holds for identical repeated calls
        var repeated = MonthlySurplusCalculator.CalculateSeries(
            account, YM(2026, 11), today, [novIncome, octIncome], [], [decExpense], [], pastMonths: 1, futureMonths: 1);
        Assert.Equal(series, repeated);
        Assert.Equal(series.GetHashCode(), repeated.GetHashCode());
    }

    [Fact]
    public void CalculateSeries_WithConditionId_KeepsRealBalanceIntactAndFiltersOnlyPendingAndFutureCardInvoices()
    {
        var account = NewAccount(initialBalance: 1000m, initialDate: Day(2026, 10, 1));
        var today = Day(2026, 11, 15);
        var momConditionId = Guid.NewGuid();

        // Historical Oct: "Outros" installment due Oct 12 (200) -> already settled in RealBalance(Oct 31) = 800
        var octOthers = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, ActiveCardId, Day(2026, 9, 10), M(200m), 1, 1,
            Rule3_12, SystemPurchaseConditions.OthersId, "Out Outros", SystemCategories.ShoppingId);

        // Current month Nov, already settled <= today (DueDate Nov 12 <= Nov 15): "Outros" 100 -> already left bank cash!
        // RealBalance(today Nov 15) = 1000 - 200 - 100 = 700 (MUST remain 700 even when filtering by OwnId!)
        var novSettledOthers = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, ActiveCardId, Day(2026, 10, 10), M(100m), 1, 1,
            Rule3_12, SystemPurchaseConditions.OthersId, "Nov Pago Outros", SystemCategories.ShoppingId);

        // Current month Nov, pending > today (DueDate Nov 20 > Nov 15): Own 150 + Mom 250 = 400
        var novPendingOwn = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, ActiveCardId, Day(2026, 10, 15), M(150m), 1, 1,
            Rule10_20, SystemPurchaseConditions.OwnId, "Nov Própria", SystemCategories.ShoppingId);
        var novPendingMom = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, ActiveCardId, Day(2026, 10, 16), M(250m), 1, 1,
            Rule10_20, momConditionId, "Nov Mãe", SystemCategories.ShoppingId);

        // Future month Dec (DueDate Dec 12): Own 200 + Others 300 = 500
        var decOwn = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, ActiveCardId, Day(2026, 11, 10), M(200m), 1, 1,
            Rule3_12, SystemPurchaseConditions.OwnId, "Dez Própria", SystemCategories.ShoppingId);
        var decOthers = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, ActiveCardId, Day(2026, 11, 11), M(300m), 1, 1,
            Rule3_12, SystemPurchaseConditions.OthersId, "Dez Outros", SystemCategories.ShoppingId);

        CreditCardPurchase[] purchases = [octOthers, novSettledOthers, novPendingOwn, novPendingMom, decOwn, decOthers];

        var realToday = RealBalanceCalculator.Calculate(account, today, [], [], [], purchases);
        var unfiltered = MonthlySurplusCalculator.CalculateSeries(
            account, YM(2026, 11), today, [], [], [], purchases, pastMonths: 1, futureMonths: 1);
        var ownOnly = MonthlySurplusCalculator.CalculateSeries(
            account, YM(2026, 11), today, [], [], [], purchases, pastMonths: 1, futureMonths: 1, conditionId: SystemPurchaseConditions.OwnId);

        Assert.Null(unfiltered.ConditionId);
        Assert.Equal(SystemPurchaseConditions.OwnId, ownOnly.ConditionId);
        Assert.NotEqual(unfiltered, ownOnly);

        // RealBalance on today (700) and historical Oct (800) are NEVER altered or masked
        Assert.Equal(M(700m), realToday.Balance);
        Assert.Equal(M(800m), unfiltered.Points[0].ProjectedSurplus);
        Assert.Equal(M(800m), ownOnly.Points[0].ProjectedSurplus);
        Assert.Equal(realToday.Balance, unfiltered.Points[1].StartingCashBalance);
        Assert.Equal(realToday.Balance, ownOnly.Points[1].StartingCashBalance);

        // Nov filtered: 700 - 150 (only Own pending > today) = 550 (vs unfiltered 700 - 400 = 300)
        Assert.Equal(M(400m), unfiltered.Points[1].CardInvoiceDue);
        Assert.Equal(M(300m), unfiltered.Points[1].ProjectedSurplus);
        Assert.Equal(M(150m), ownOnly.Points[1].CardInvoiceDue);
        Assert.Equal(M(550m), ownOnly.Points[1].ProjectedSurplus);

        // Dec filtered rollover: starts 550 - 200 (only Own) = 350 (vs unfiltered 300 - 500 = -200)
        Assert.Equal(M(300m), unfiltered.Points[2].StartingCashBalance);
        Assert.Equal(M(500m), unfiltered.Points[2].CardInvoiceDue);
        Assert.Equal(M(-200m), unfiltered.Points[2].ProjectedSurplus);

        Assert.Equal(M(550m), ownOnly.Points[2].StartingCashBalance);
        Assert.Equal(M(200m), ownOnly.Points[2].CardInvoiceDue);
        Assert.Equal(M(350m), ownOnly.Points[2].ProjectedSurplus);

        // Telescoping invariant still holds under condition filter: 1000 + (-200 - 250 - 200) = 350
        Assert.Equal(
            ownOnly.Points[^1].ProjectedSurplus,
            account.InitialBalance + Money.Sum(ownOnly.Points.Select(p => p.NetMonthlyVariation)));

        // Empty ConditionId rejected at boundary
        Assert.Throws<ArgumentException>(() => MonthlySurplusCalculator.CalculateSeries(
            account, YM(2026, 11), today, [], [], [], purchases, conditionId: Guid.Empty));
    }

    [Fact]
    public void CalculateSeries_DoesNotCarryOverOverduePendingsFromPreviousMonths_AndSeparatesDueDateFromAccrualProjectedResult()
    {
        var account = NewAccount(initialBalance: 1000m, initialDate: Day(2026, 10, 1));
        var today = Day(2026, 11, 15);

        // Overdue unreceived income from Oct (must NOT be dragged into Nov!)
        var overdueOctIncome = Income.Create(Guid.NewGuid(), AccountId, Day(2026, 10, 20), M(900m), "Freela atrasado Out");
        // Overdue unpaid fixed expense from Oct only (must NOT be dragged into Nov!)
        var overdueOctFixed = FixedExpense.Create(
            Guid.NewGuid(), AccountId, YM(2026, 10), YM(2026, 10), M(400m), "Seguro Out", SystemCategories.ServicesId);

        // Direct expenses in Nov: Nov 10 <= today (already in cash), Nov 15 == today (already in cash), Nov 20 > today (in cascade)
        var directBefore = DirectExpense.Create(Guid.NewGuid(), AccountId, Day(2026, 11, 10), M(100m), "Mercado", SystemCategories.FoodId);
        var directToday = DirectExpense.Create(Guid.NewGuid(), AccountId, Day(2026, 11, 15), M(50m), "Café", SystemCategories.FoodId);
        var directFuture = DirectExpense.Create(Guid.NewGuid(), AccountId, Day(2026, 11, 20), M(75m), "Consulta", SystemCategories.HealthId);

        // Rule (25, 5): purchase on Nov 10 closes Nov 25 (Nov accrual!) and is due Dec 5 (Dec cash surplus!)
        var crossMonthPurchase = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, ActiveCardId, Day(2026, 11, 10), M(300m), 1, 1,
            Rule25_5, SystemPurchaseConditions.OwnId, "Passagem", SystemCategories.TransportId);

        var series = MonthlySurplusCalculator.CalculateSeries(
            account,
            YM(2026, 11),
            today,
            [overdueOctIncome],
            [directBefore, directToday, directFuture],
            [overdueOctFixed],
            [crossMonthPurchase],
            pastMonths: 1,
            futureMonths: 1);

        var novSurplus = series.SelectedMonth;
        // Cash on Nov 15 = 1000 - 100 - 50 = 850; no overdue Oct income or Oct fixed dragged into Nov
        Assert.Equal(M(850m), novSurplus.StartingCashBalance);
        Assert.Equal(Money.Zero, novSurplus.ExpectedIncomes);
        Assert.Equal(Money.Zero, novSurplus.FixedExpenses);
        Assert.Equal(Money.Zero, novSurplus.CardInvoiceDue); // Due Dec 5 -> NOT in Nov surplus
        Assert.Equal(M(75m), novSurplus.DirectExpenses);     // Only Date > today
        Assert.Equal(M(775m), novSurplus.ProjectedSurplus);

        // Dec cash surplus carries 775 and pays the 300 invoice due Dec 5 -> 475
        var decSurplus = series.Points[2];
        Assert.Equal(M(775m), decSurplus.StartingCashBalance);
        Assert.Equal(M(300m), decSurplus.CardInvoiceDue);
        Assert.Equal(M(475m), decSurplus.ProjectedSurplus);

        // Pure accrual ProjectedResult for Nov includes the Nov-closing invoice (300) and all Nov direct expenses (225)
        var novAccrual = ProjectedResultCalculator.Calculate(
            AccountId,
            YM(2026, 11),
            [overdueOctIncome],
            [directBefore, directToday, directFuture],
            [overdueOctFixed],
            [crossMonthPurchase]);
        Assert.Equal(M(300m), novAccrual.CardInvoice);
        Assert.Equal(M(225m), novAccrual.DirectExpenses);
        Assert.Equal(M(-525m), novAccrual.Result);
    }
}
