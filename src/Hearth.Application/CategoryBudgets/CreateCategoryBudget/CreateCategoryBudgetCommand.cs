namespace Hearth.Application.CategoryBudgets.CreateCategoryBudget;

public sealed record CreateCategoryBudgetCommand(
    Guid AccountId,
    Guid CategoryId,
    int StartYear,
    int StartMonth,
    int? EndYear,
    int? EndMonth,
    decimal Amount);
