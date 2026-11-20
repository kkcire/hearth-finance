using Hearth.Domain.ValueObjects;

namespace Hearth.Application.DirectExpenses.RegisterDirectExpense;

public sealed record RegisterDirectExpenseCommand(
    Guid AccountId,
    DateOnly Date,
    decimal Amount,
    string Description,
    ExpenseCategory Category);
