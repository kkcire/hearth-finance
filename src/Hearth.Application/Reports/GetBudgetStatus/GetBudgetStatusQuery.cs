namespace Hearth.Application.Reports.GetBudgetStatus;

public sealed record GetBudgetStatusQuery(
    Guid AccountId,
    int Year,
    int Month,
    bool IncludeDirectExpenses = true,
    bool IncludeFixedExpenses = true,
    bool IncludeCardInstallments = true,
    IReadOnlyList<Guid>? IncludedConditionIds = null);
