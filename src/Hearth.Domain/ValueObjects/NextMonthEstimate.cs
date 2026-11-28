namespace Hearth.Domain.ValueObjects;

/// <summary>
/// Isolated monthly estimate for the month immediately following the selected month (M + 1),
/// considering expected incomes, active fixed expenses, scheduled credit card installments,
/// and scheduled direct expenses for that month.
/// When viewing the current calendar month without a condition filter, also carries the
/// projected cash opening balance entering M + 1 and the projected cash ending balance at
/// the close of M + 1 without double-counting already settled items.
/// </summary>
public sealed record NextMonthEstimate(
    YearMonth NextYearMonth,
    Money EstimatedIncome,
    Money EstimatedFixedExpenses,
    Money EstimatedCardInstallments,
    Money EstimatedDirectExpenses,
    Money EstimatedCommitments,
    Money ProjectedResult,
    decimal EstimatedSavingsRatePercentage,
    Money? ProjectedOpeningBalance = null,
    Money? ProjectedEndingBalance = null);
