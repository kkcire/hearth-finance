namespace Hearth.Application.DirectExpenses.UpdateDirectExpense;

public sealed record UpdateDirectExpenseCommand(
    Guid DirectExpenseId,
    DateOnly Date,
    decimal Amount,
    string Description,
    Guid CategoryId);
