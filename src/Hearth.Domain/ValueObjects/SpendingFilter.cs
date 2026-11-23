namespace Hearth.Domain.ValueObjects;

/// <summary>
/// Orthogonal source and credit-card-condition filters applied to accrual spending and budget
/// status calculations. Always valid: condition lists contain no null entries.
/// </summary>
public sealed class SpendingFilter
{
    private readonly HashSet<PurchaseCondition>? _includedConditions;

    private SpendingFilter(
        bool includeDirectExpenses,
        bool includeFixedExpenses,
        bool includeCardInstallments,
        HashSet<PurchaseCondition>? includedConditions)
    {
        IncludeDirectExpenses = includeDirectExpenses;
        IncludeFixedExpenses = includeFixedExpenses;
        IncludeCardInstallments = includeCardInstallments;
        _includedConditions = includedConditions;
    }

    public static SpendingFilter All { get; } = new(true, true, true, null);

    public bool IncludeDirectExpenses { get; }

    public bool IncludeFixedExpenses { get; }

    public bool IncludeCardInstallments { get; }

    public IReadOnlyCollection<PurchaseCondition>? IncludedConditions => _includedConditions;

    public bool ShouldProcessCardInstallments =>
        IncludeCardInstallments && (_includedConditions is null || _includedConditions.Count > 0);

    public bool MatchesCondition(PurchaseCondition condition) =>
        _includedConditions is null || _includedConditions.Contains(condition);

    public static SpendingFilter Create(
        bool includeDirectExpenses = true,
        bool includeFixedExpenses = true,
        bool includeCardInstallments = true,
        IReadOnlyCollection<PurchaseCondition>? includedConditions = null)
    {
        HashSet<PurchaseCondition>? conditionSet = null;
        if (includedConditions is not null)
        {
            conditionSet = new HashSet<PurchaseCondition>(includedConditions.Count);
            foreach (var condition in includedConditions)
            {
                if (condition is null)
                {
                    throw new ArgumentException(
                        "The included conditions must not contain null entries.",
                        nameof(includedConditions));
                }

                conditionSet.Add(condition);
            }
        }

        return new SpendingFilter(
            includeDirectExpenses,
            includeFixedExpenses,
            includeCardInstallments,
            conditionSet);
    }
}
