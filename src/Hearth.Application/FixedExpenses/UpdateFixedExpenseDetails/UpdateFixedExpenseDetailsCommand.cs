namespace Hearth.Application.FixedExpenses.UpdateFixedExpenseDetails;

public sealed record UpdateFixedExpenseDetailsCommand(
    Guid FixedExpenseId,
    string Description,
    Guid CategoryId);
