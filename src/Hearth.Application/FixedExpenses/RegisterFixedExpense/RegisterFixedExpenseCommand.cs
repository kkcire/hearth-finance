using Hearth.Domain.ValueObjects;

namespace Hearth.Application.FixedExpenses.RegisterFixedExpense;

public sealed record RegisterFixedExpenseCommand(
    Guid AccountId,
    int StartYear,
    int StartMonth,
    int? EndYear,
    int? EndMonth,
    decimal Amount,
    string Description,
    ExpenseCategory Category);
