namespace Hearth.Domain.ValueObjects;

/// <summary>
/// Isolated financial flow for a single calendar month: income minus total monthly expenses,
/// without carrying over surpluses from unclosed future months.
/// </summary>
public sealed record MonthlyFlowSummary(
    YearMonth Month,
    Money MonthlyIncome,
    Money OwnMonthlyIncome,
    Money DirectExpenses,
    Money FixedExpenses,
    Money CardInvoice,
    Money TotalExpenses,
    Money MonthlyResult,
    decimal SavingsRatePercentage,
    BreakEvenStatus BreakEvenStatus,
    Money CommittedFloor,
    bool IsProjection);
