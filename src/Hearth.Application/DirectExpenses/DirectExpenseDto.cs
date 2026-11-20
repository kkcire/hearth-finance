using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.DirectExpenses;

public sealed record DirectExpenseDto(
    Guid Id,
    Guid AccountId,
    DateOnly Date,
    decimal Amount,
    string Description,
    ExpenseCategory Category)
{
    internal static DirectExpenseDto FromEntity(DirectExpense expense) =>
        new(
            expense.Id,
            expense.AccountId,
            expense.Date,
            expense.Amount.Amount,
            expense.Description,
            expense.Category);
}
