using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Reports.GetExpensesByCategory;

public sealed record PurchaseConditionFilter(PurchaseConditionKind Kind, string? Name = null);

public sealed record GetExpensesByCategoryQuery(
    Guid AccountId,
    int Year,
    int Month,
    bool IncludeDirectExpenses = true,
    bool IncludeFixedExpenses = true,
    bool IncludeCardInstallments = true,
    IReadOnlyList<PurchaseConditionFilter>? IncludedConditions = null);
