namespace Hearth.Application.CategoryBudgets.ChangeCategoryBudgetAmount;

public sealed record ChangeCategoryBudgetAmountCommand(
    Guid CategoryBudgetId,
    decimal NewAmount);
