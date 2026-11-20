namespace Hearth.Application.CategoryBudgets.EndCategoryBudget;

public sealed record EndCategoryBudgetCommand(
    Guid CategoryBudgetId,
    int LastYear,
    int LastMonth);
