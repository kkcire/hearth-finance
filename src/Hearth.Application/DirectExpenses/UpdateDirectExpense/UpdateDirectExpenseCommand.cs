using Hearth.Domain.ValueObjects;

namespace Hearth.Application.DirectExpenses.UpdateDirectExpense;

public sealed record UpdateDirectExpenseCommand(
    Guid DirectExpenseId,
    DateOnly Date,
    decimal Amount,
    string Description,
    ExpenseCategory Category);
