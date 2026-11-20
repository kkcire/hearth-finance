namespace Hearth.Application.FixedExpenses.MarkFixedExpenseAsPaid;

public sealed record MarkFixedExpenseAsPaidCommand(
    Guid FixedExpenseId,
    int Year,
    int Month,
    DateOnly PaidOn);
