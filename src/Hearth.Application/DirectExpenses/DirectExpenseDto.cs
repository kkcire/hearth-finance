using Hearth.Domain.Entities;

namespace Hearth.Application.DirectExpenses;

public sealed record DirectExpenseDto(
    Guid Id,
    Guid AccountId,
    DateOnly Date,
    decimal Amount,
    string Description,
    Guid CategoryId)
{
    internal static DirectExpenseDto FromEntity(DirectExpense expense) =>
        new(
            expense.Id,
            expense.AccountId,
            expense.Date,
            expense.Amount.Amount,
            expense.Description,
            expense.CategoryId);
}
