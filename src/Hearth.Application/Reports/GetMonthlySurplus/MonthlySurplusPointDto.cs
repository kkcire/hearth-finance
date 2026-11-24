using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Reports.GetMonthlySurplus;

public sealed record MonthlySurplusPointDto(
    int Year,
    int Month,
    bool IsHistorical,
    bool IsCurrentMonth,
    decimal StartingCashBalance,
    decimal ExpectedIncomes,
    decimal TotalAvailable,
    decimal CardInvoiceDue,
    decimal FixedExpenses,
    decimal DirectExpenses,
    decimal TotalDiscounts,
    decimal ProjectedSurplus,
    decimal NetMonthlyVariation)
{
    /// <summary>
    /// True on the current month, where <see cref="DirectExpenses"/> counts only future/scheduled
    /// direct expenses (<c>Date &gt; today</c>) because earlier ones are already inside <see cref="StartingCashBalance"/>.
    /// </summary>
    public bool DirectExpensesAreFutureOnly => IsCurrentMonth;

    internal static MonthlySurplusPointDto FromDomain(MonthlySurplus point) =>
        new(
            point.Month.Year,
            point.Month.Month,
            point.IsHistorical,
            point.IsCurrentMonth,
            point.StartingCashBalance.Amount,
            point.ExpectedIncomes.Amount,
            point.TotalAvailable.Amount,
            point.CardInvoiceDue.Amount,
            point.FixedExpenses.Amount,
            point.DirectExpenses.Amount,
            point.TotalDiscounts.Amount,
            point.ProjectedSurplus.Amount,
            point.NetMonthlyVariation.Amount);
}
