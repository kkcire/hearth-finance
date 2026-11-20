using Hearth.Domain.ValueObjects;

namespace Hearth.Application.FixedExpenses.UpdateFixedExpenseDetails;

public sealed record UpdateFixedExpenseDetailsCommand(
    Guid FixedExpenseId,
    string Description,
    ExpenseCategory Category);
