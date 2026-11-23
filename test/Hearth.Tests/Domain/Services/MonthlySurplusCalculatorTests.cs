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
            Rule3_12, PurchaseCondition.Own, "Fatura hoje", SystemCategories.ShoppingId);

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
            Rule10_20, PurchaseCondition.Own, "Fatura futura", SystemCategories.ShoppingId);

        // Noise from another account and previously-paid installment -> must be ignored
        var otherIncome = Income.Create(Guid.NewGuid(), OtherAccountId, Day(2026, 11, 25), M(9999m), "Outra conta");
        var previouslyPaidCard = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, ActiveCardId, Day(2026, 9, 15), M(600m), 2, 2,
            Rule10_20, PurchaseCondition.Own, "Em andamento", SystemCategories.ShoppingId);
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
            Rule25_5, PurchaseCondition.Own, "Notebook", SystemCategories.ShoppingId);

        // Archived card purchase on Nov 10 (300 in 1x, rule 3/12): closes Dec 3, DueDate Dec 12 -> 300
        var archivedCardPurchase = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, ArchivedCardId, Day(2026, 11, 10), M(300m), 1, 1,
            Rule3_12, PurchaseCondition.Others, "Cartão arquivado", SystemCategories.ShoppingId);

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
            Rule3_12, PurchaseCondition.Own, "Tênis", SystemCategories.ShoppingId); // Due Oct 12

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
}
