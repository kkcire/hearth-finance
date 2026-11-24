namespace Hearth.Application.Reports.GetExpensesByCategory;

public sealed record GetExpensesByCategoryQuery(
    Guid AccountId,
    int Year,
    int Month,
    bool IncludeDirectExpenses = true,
    bool IncludeFixedExpenses = true,
    bool IncludeCardInstallments = true,
    IReadOnlyList<Guid>? IncludedConditionIds = null);
