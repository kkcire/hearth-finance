using Hearth.Domain.Entities;

namespace Hearth.Application.CategoryBudgets;

public sealed record CategoryBudgetAmountChangeDto(
    int EffectiveFromYear,
    int EffectiveFromMonth,
    decimal Amount);

public sealed record CategoryBudgetDto(
    Guid Id,
    Guid AccountId,
    Guid CategoryId,
    string CategoryName,
    int StartYear,
    int StartMonth,
    int? EndYear,
    int? EndMonth,
    IReadOnlyList<CategoryBudgetAmountChangeDto> AmountHistory)
{
    internal static CategoryBudgetDto FromEntity(CategoryBudget budget, string categoryName) =>
        new(
            budget.Id,
            budget.AccountId,
            budget.CategoryId,
            categoryName,
            budget.StartMonth.Year,
            budget.StartMonth.Month,
            budget.EndMonth?.Year,
            budget.EndMonth?.Month,
            budget.AmountHistory
                .Select(change => new CategoryBudgetAmountChangeDto(
                    change.EffectiveFrom.Year,
                    change.EffectiveFrom.Month,
                    change.Amount.Amount))
                .ToList());
}
