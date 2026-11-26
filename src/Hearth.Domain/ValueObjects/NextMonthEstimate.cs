namespace Hearth.Domain.ValueObjects;

/// <summary>
/// Strictly isolated estimate for the month immediately following the selected month (M + 1),
/// considering expected incomes, active fixed expenses, scheduled credit card
/// installments, and scheduled direct expenses for that month — never rolling cash balances
/// or unclosed surpluses from earlier periods.
/// </summary>
public sealed record NextMonthEstimate(
    YearMonth NextYearMonth,
    Money EstimatedIncome,
    Money EstimatedFixedExpenses,
    Money EstimatedCardInstallments,
    Money EstimatedDirectExpenses,
    Money EstimatedCommitments,
    Money ProjectedResult,
    decimal EstimatedSavingsRatePercentage);
