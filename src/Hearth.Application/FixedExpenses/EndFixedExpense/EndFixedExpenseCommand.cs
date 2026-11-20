namespace Hearth.Application.FixedExpenses.EndFixedExpense;

public sealed record EndFixedExpenseCommand(Guid FixedExpenseId, int LastYear, int LastMonth);
