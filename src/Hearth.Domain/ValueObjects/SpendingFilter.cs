namespace Hearth.Domain.ValueObjects;

/// <summary>
/// Orthogonal source and credit-card-condition filters applied to accrual spending and budget
/// status calculations. Always valid: condition id lists contain no empty GUID entries.
/// </summary>
public sealed class SpendingFilter
{
    private readonly HashSet<Guid>? _includedConditionIds;

    private SpendingFilter(
        bool includeDirectExpenses,
        bool includeFixedExpenses,
        bool includeCardInstallments,
        HashSet<Guid>? includedConditionIds)
    {
        IncludeDirectExpenses = includeDirectExpenses;
        IncludeFixedExpenses = includeFixedExpenses;
        IncludeCardInstallments = includeCardInstallments;
        _includedConditionIds = includedConditionIds;
    }

    public static SpendingFilter All { get; } = new(true, true, true, null);

    public bool IncludeDirectExpenses { get; }

    public bool IncludeFixedExpenses { get; }

    public bool IncludeCardInstallments { get; }

    public IReadOnlyCollection<Guid>? IncludedConditionIds => _includedConditionIds;

    public bool ShouldProcessCardInstallments =>
        IncludeCardInstallments && (_includedConditionIds is null || _includedConditionIds.Count > 0);

    public bool MatchesCondition(Guid conditionId) =>
        _includedConditionIds is null || _includedConditionIds.Contains(conditionId);

    public static SpendingFilter Create(
        bool includeDirectExpenses = true,
        bool includeFixedExpenses = true,
        bool includeCardInstallments = true,
        IReadOnlyCollection<Guid>? includedConditionIds = null)
    {
        HashSet<Guid>? conditionSet = null;
        if (includedConditionIds is not null)
        {
            conditionSet = new HashSet<Guid>(includedConditionIds.Count);
            foreach (var conditionId in includedConditionIds)
            {
                if (conditionId == Guid.Empty)
                {
                    throw new ArgumentException(
                        "The included condition ids must not contain empty GUID entries.",
                        nameof(includedConditionIds));
                }

                conditionSet.Add(conditionId);
            }
        }

        return new SpendingFilter(
            includeDirectExpenses,
            includeFixedExpenses,
            includeCardInstallments,
            conditionSet);
    }
}
