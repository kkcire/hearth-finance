namespace Hearth.Domain.ValueObjects;

/// <summary>
/// Monthly budget status of an account across all categories that have an active budget in the month.
/// Created only by <c>BudgetStatusCalculator</c>.
/// </summary>
public sealed record BudgetStatus
{
    internal BudgetStatus(
        YearMonth month,
        IReadOnlyList<BudgetStatusItem> items,
        Money totalBudgeted,
        Money totalSpent)
    {
        Month = month;
        Items = items;
        TotalBudgeted = totalBudgeted;
        TotalSpent = totalSpent;
    }

    public YearMonth Month { get; }

    /// <summary>
    /// Budgeted categories active in the month, including those with zero spending, ordered by
    /// <see cref="BudgetStatusItem.UsagePercentage"/> descending, then
    /// <see cref="BudgetStatusItem.SpentAmount"/> descending, then <see cref="BudgetStatusItem.CategoryId"/> ascending.
    /// </summary>
    public IReadOnlyList<BudgetStatusItem> Items { get; }

    /// <summary>Sum of monthly targets across all budgeted categories active in the month.</summary>
    public Money TotalBudgeted { get; }

    /// <summary>Sum of spending across all budgeted categories active in the month.</summary>
    public Money TotalSpent { get; }

    /// <summary>Total remaining across budgeted categories. Negative when overall budgeted spending exceeds total budgeted.</summary>
    public Money TotalRemaining => TotalBudgeted - TotalSpent;

    public bool Equals(BudgetStatus? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Month == other.Month
               && TotalBudgeted == other.TotalBudgeted
               && TotalSpent == other.TotalSpent
               && Items.SequenceEqual(other.Items);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Month);
        hash.Add(TotalBudgeted);
        hash.Add(TotalSpent);
        foreach (var item in Items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }
}
