using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Services;

/// <summary>
/// Calculates the cash-basis monthly surplus projection cascade and multi-month evolution series.
/// Starting from the real cash balance on <c>today</c>, future months roll over month by month
/// without double-counting any item already settled on or before <c>today</c>.
/// </summary>
public static class MonthlySurplusCalculator
{
    public const int DefaultPastMonths = 6;
    public const int DefaultFutureMonths = 5;

    /// <summary>
    /// Computes the monthly surplus cascade for <paramref name="selectedMonth"/> and the
    /// chronological evolution series anchored on <paramref name="today"/>.
    /// </summary>
    public static MonthlySurplusSeries CalculateSeries(
        Account account,
        YearMonth selectedMonth,
        DateOnly today,
        IEnumerable<Income> incomes,
        IEnumerable<DirectExpense> directExpenses,
        IEnumerable<FixedExpense> fixedExpenses,
        IEnumerable<CreditCardPurchase> purchases,
        int pastMonths = DefaultPastMonths,
        int futureMonths = DefaultFutureMonths)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(selectedMonth);
        ArgumentNullException.ThrowIfNull(incomes);
        ArgumentNullException.ThrowIfNull(directExpenses);
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(purchases);

        if (pastMonths < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pastMonths), pastMonths, "Past months cannot be negative.");
        }

        if (futureMonths < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(futureMonths), futureMonths, "Future months cannot be negative.");
        }

        var data = AccountCashData.FilterFor(account, incomes, directExpenses, fixedExpenses, purchases);
        var currentMonth = YearMonth.From(today);
        var initialMonth = YearMonth.From(account.InitialBalanceDate);

        var windowStart = DetermineWindowStart(currentMonth, initialMonth, pastMonths);
        var windowEnd = ClampAddMonths(currentMonth, futureMonths);

        var historicalPoints = BuildHistoricalWindow(data, windowStart, currentMonth, initialMonth);
        var currentOpeningCash = DetermineCurrentOpeningCash(data, historicalPoints, currentMonth, initialMonth);
        var currentPoint = BuildCurrentMonth(data, currentMonth, today, currentOpeningCash);

        var projectionEnd = selectedMonth > windowEnd ? selectedMonth : windowEnd;
        var futurePoints = BuildFutureMonths(data, currentPoint, projectionEnd, today);

        var seriesPoints = AssembleSeries(historicalPoints, currentPoint, futurePoints, windowEnd);
        var selectedPoint = ResolveSelectedMonth(
            data, selectedMonth, initialMonth, currentMonth, historicalPoints, currentPoint, futurePoints);
        var averageVariation = CalculateAverageVariation(seriesPoints);

        return new MonthlySurplusSeries(today, selectedPoint, seriesPoints, averageVariation);
    }

    private static YearMonth DetermineWindowStart(YearMonth currentMonth, YearMonth initialMonth, int pastMonths)
    {
        if (currentMonth < initialMonth)
        {
            return currentMonth;
        }

        var desired = ClampAddMonths(currentMonth, -pastMonths);
        return desired < initialMonth ? initialMonth : desired;
    }

    private static List<MonthlySurplus> BuildHistoricalWindow(
        AccountCashData data,
        YearMonth windowStart,
        YearMonth currentMonth,
        YearMonth initialMonth)
    {
        var points = new List<MonthlySurplus>();
        if (windowStart >= currentMonth)
        {
            return points;
        }

        var openingCash = CalculateOpeningCash(data, windowStart, initialMonth);
        for (var month = windowStart; month < currentMonth; month = month.AddMonths(1))
        {
            var point = BuildHistoricalMonth(data, month, openingCash);
            points.Add(point);
            openingCash = point.ProjectedSurplus;
        }

        return points;
    }

    private static Money DetermineCurrentOpeningCash(
        AccountCashData data,
        List<MonthlySurplus> historicalPoints,
        YearMonth currentMonth,
        YearMonth initialMonth)
    {
        if (historicalPoints.Count > 0)
        {
            return historicalPoints[^1].ProjectedSurplus;
        }

        return CalculateOpeningCash(data, currentMonth, initialMonth);
    }

    private static Money CalculateOpeningCash(
        AccountCashData data,
        YearMonth month,
        YearMonth initialMonth)
    {
        if (month <= initialMonth)
        {
            return data.Account.InitialBalance;
        }

        var previousDay = month.AddMonths(-1).LastDay;
        return RealBalanceCalculator.Calculate(
            data.Account,
            previousDay,
            data.Incomes,
            data.DirectExpenses,
            data.FixedExpenses,
            data.Purchases).Balance;
    }

    private static MonthlySurplus BuildHistoricalMonth(
        AccountCashData data,
        YearMonth month,
        Money openingCash)
    {
        var receivedIncomes = Money.Sum(
            data.Incomes
                .Where(income => income.ReceivedOn is { } received
                                 && YearMonth.From(received) == month
                                 && data.Account.CanRegisterOn(received))
                .Select(income => income.Amount));

        var cardInvoiceDue = Money.Sum(
            data.ActiveInstallments
                .Where(installment => installment.Cycle.DueMonth == month
                                      && data.Account.CanRegisterOn(installment.Cycle.DueDate))
                .Select(installment => installment.Amount));

        var fixedExpenses = Money.Sum(
            data.FixedExpenses
                .SelectMany(expense => expense.GetPaymentEntries().Select(payment => (Expense: expense, Payment: payment)))
                .Where(item => YearMonth.From(item.Payment.PaidOn) == month
                               && data.Account.CanRegisterOn(item.Payment.PaidOn))
                .Select(item => item.Expense.AmountIn(item.Payment.Month)
                                ?? throw new InvalidOperationException(
                                    "A paid fixed expense has no amount in the month it was paid.")));

        var directExpenses = Money.Sum(
            data.DirectExpenses
                .Where(expense => expense.Month == month && data.Account.CanRegisterOn(expense.Date))
                .Select(expense => expense.Amount));

        var closing = openingCash + receivedIncomes - cardInvoiceDue - fixedExpenses - directExpenses;
        var netVariation = closing - openingCash;

        return new MonthlySurplus(
            month,
            isHistorical: true,
            isCurrentMonth: false,
            openingCash,
            receivedIncomes,
            cardInvoiceDue,
            fixedExpenses,
            directExpenses,
            netVariation);
    }

    private static MonthlySurplus BuildCurrentMonth(
        AccountCashData data,
        YearMonth currentMonth,
        DateOnly today,
        Money openingCashOfMonth)
    {
        var currentCash = RealBalanceCalculator.Calculate(
            data.Account,
            today,
            data.Incomes,
            data.DirectExpenses,
            data.FixedExpenses,
            data.Purchases).Balance;

        return BuildProjectedMonth(
            data,
            currentMonth,
            isCurrentMonth: true,
            startingCashBalance: currentCash,
            openingCashOfMonth: openingCashOfMonth,
            cutoffDate: today);
    }

    private static List<MonthlySurplus> BuildFutureMonths(
        AccountCashData data,
        MonthlySurplus currentPoint,
        YearMonth projectionEnd,
        DateOnly today)
    {
        var points = new List<MonthlySurplus>();
        if (projectionEnd <= currentPoint.Month)
        {
            return points;
        }

        var carryBalance = currentPoint.ProjectedSurplus;
        var month = currentPoint.Month.AddMonths(1);

        while (true)
        {
            var point = BuildProjectedMonth(
                data,
                month,
                isCurrentMonth: false,
                startingCashBalance: carryBalance,
                openingCashOfMonth: carryBalance,
                cutoffDate: today);
            points.Add(point);
            carryBalance = point.ProjectedSurplus;

            if (month == projectionEnd)
            {
                break;
            }

            month = month.AddMonths(1);
        }

        return points;
    }

    private static MonthlySurplus BuildProjectedMonth(
        AccountCashData data,
        YearMonth month,
        bool isCurrentMonth,
        Money startingCashBalance,
        Money openingCashOfMonth,
        DateOnly cutoffDate)
    {
        var expectedIncomes = Money.Sum(
            data.Incomes
                .Where(income => income.ExpectedMonth == month
                                 && data.Account.CanRegisterOn(income.ExpectedDate)
                                 && !IsSettledOnOrBefore(income.ReceivedOn, cutoffDate))
                .Select(income => income.Amount));

        var cardInvoiceDue = Money.Sum(
            data.ActiveInstallments
                .Where(installment => installment.Cycle.DueMonth == month
                                      && installment.Cycle.DueDate > cutoffDate
                                      && data.Account.CanRegisterOn(installment.Cycle.DueDate))
                .Select(installment => installment.Amount));

        var fixedExpenses = data.Account.CanRegisterOn(month.LastDay)
            ? Money.Sum(
                data.FixedExpenses
                    .Where(expense => !IsSettledOnOrBefore(expense.PaidOn(month), cutoffDate))
                    .Select(expense => expense.AmountIn(month))
                    .Where(amount => amount.HasValue)
                    .Select(amount => amount!.Value))
            : Money.Zero;

        var directExpenses = Money.Sum(
            data.DirectExpenses
                .Where(expense => expense.Month == month
                                  && expense.Date > cutoffDate
                                  && data.Account.CanRegisterOn(expense.Date))
                .Select(expense => expense.Amount));

        var projectedSurplus = startingCashBalance + expectedIncomes - cardInvoiceDue - fixedExpenses - directExpenses;
        var netVariation = projectedSurplus - openingCashOfMonth;

        return new MonthlySurplus(
            month,
            isHistorical: false,
            isCurrentMonth: isCurrentMonth,
            startingCashBalance,
            expectedIncomes,
            cardInvoiceDue,
            fixedExpenses,
            directExpenses,
            netVariation);
    }

    private static List<MonthlySurplus> AssembleSeries(
        List<MonthlySurplus> historicalPoints,
        MonthlySurplus currentPoint,
        List<MonthlySurplus> futurePoints,
        YearMonth windowEnd)
    {
        var series = new List<MonthlySurplus>(historicalPoints.Count + 1 + futurePoints.Count);
        series.AddRange(historicalPoints);
        series.Add(currentPoint);

        foreach (var future in futurePoints)
        {
            if (future.Month <= windowEnd)
            {
                series.Add(future);
            }
        }

        return series;
    }

    private static MonthlySurplus ResolveSelectedMonth(
        AccountCashData data,
        YearMonth selectedMonth,
        YearMonth initialMonth,
        YearMonth currentMonth,
        List<MonthlySurplus> historicalPoints,
        MonthlySurplus currentPoint,
        List<MonthlySurplus> futurePoints)
    {
        if (selectedMonth == currentMonth)
        {
            return currentPoint;
        }

        if (selectedMonth > currentMonth)
        {
            return futurePoints.First(point => point.Month == selectedMonth);
        }

        var existingHistorical = historicalPoints.FirstOrDefault(point => point.Month == selectedMonth);
        if (existingHistorical is not null)
        {
            return existingHistorical;
        }

        var opening = CalculateOpeningCash(data, selectedMonth, initialMonth);
        return BuildHistoricalMonth(data, selectedMonth, opening);
    }

    private static Money CalculateAverageVariation(IReadOnlyList<MonthlySurplus> points)
    {
        if (points.Count == 0)
        {
            return Money.Zero;
        }

        var totalVariation = Money.Sum(points.Select(point => point.NetMonthlyVariation));
        var rounded = decimal.Round(totalVariation.Amount / points.Count, 2, MidpointRounding.AwayFromZero);
        return Money.FromDecimal(rounded);
    }

    private static bool IsSettledOnOrBefore(DateOnly? settledOn, DateOnly cutoffDate) =>
        settledOn is { } date && date <= cutoffDate;

    private static YearMonth ClampAddMonths(YearMonth origin, int delta)
    {
        const long minIndex = (long)YearMonth.MinYear * 12;
        const long maxIndex = ((long)YearMonth.MaxYear * 12) + 11;

        var targetIndex = Math.Clamp(((long)origin.Year * 12) + (origin.Month - 1) + delta, minIndex, maxIndex);
        return YearMonth.Of((int)(targetIndex / 12), (int)(targetIndex % 12) + 1);
    }

    private sealed class AccountCashData
    {
        private AccountCashData(
            Account account,
            IReadOnlyList<Income> incomes,
            IReadOnlyList<DirectExpense> directExpenses,
            IReadOnlyList<FixedExpense> fixedExpenses,
            IReadOnlyList<CreditCardPurchase> purchases,
            IReadOnlyList<Installment> activeInstallments)
        {
            Account = account;
            Incomes = incomes;
            DirectExpenses = directExpenses;
            FixedExpenses = fixedExpenses;
            Purchases = purchases;
            ActiveInstallments = activeInstallments;
        }

        public Account Account { get; }

        public IReadOnlyList<Income> Incomes { get; }

        public IReadOnlyList<DirectExpense> DirectExpenses { get; }

        public IReadOnlyList<FixedExpense> FixedExpenses { get; }

        public IReadOnlyList<CreditCardPurchase> Purchases { get; }

        public IReadOnlyList<Installment> ActiveInstallments { get; }

        public static AccountCashData FilterFor(
            Account account,
            IEnumerable<Income> incomes,
            IEnumerable<DirectExpense> directExpenses,
            IEnumerable<FixedExpense> fixedExpenses,
            IEnumerable<CreditCardPurchase> purchases)
        {
            var accountIncomes = incomes.Where(income => income.AccountId == account.Id).ToList();
            var accountDirects = directExpenses.Where(expense => expense.AccountId == account.Id).ToList();
            var accountFixed = fixedExpenses.Where(expense => expense.AccountId == account.Id).ToList();
            var accountPurchases = purchases.Where(purchase => purchase.AccountId == account.Id).ToList();
            var activeInstallments = accountPurchases
                .SelectMany(purchase => purchase.GetActiveInstallments())
                .ToList();

            return new AccountCashData(
                account,
                accountIncomes,
                accountDirects,
                accountFixed,
                accountPurchases,
                activeInstallments);
        }
    }
}
