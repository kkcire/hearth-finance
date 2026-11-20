namespace Hearth.Domain.ValueObjects;

/// <summary>
/// The spending of an account in a month grouped by category (accrual basis), with per-source totals.
/// Created only by <c>CategorySpendingCalculator</c>.
/// </summary>
public sealed record CategorySpending
{
    internal CategorySpending(
        YearMonth month,
        IReadOnlyList<CategorySpendingItem> categories,
        Money directExpenses,
        Money fixedExpenses,
        Money cardInstallments)
    {
        Month = month;
        Categories = categories;
        DirectExpenses = directExpenses;
        FixedExpenses = fixedExpenses;
        CardInstallments = cardInstallments;
    }

    public YearMonth Month { get; }

    /// <summary>
    /// Categories with spending greater than zero, ordered by <see cref="CategorySpendingItem.Total"/>
    /// descending and then by <see cref="CategorySpendingItem.Category"/> ascending.
    /// </summary>
    public IReadOnlyList<CategorySpendingItem> Categories { get; }

    /// <summary>Total direct expenses across all included categories.</summary>
    public Money DirectExpenses { get; }

    /// <summary>Total fixed expenses across all included categories.</summary>
    public Money FixedExpenses { get; }

    /// <summary>Total card installments across all included categories.</summary>
    public Money CardInstallments { get; }

    /// <summary>
    /// Total spending across all included categories and sources.
    /// Derived from the components, so it can never disagree with them.
    /// </summary>
    public Money Total => DirectExpenses + FixedExpenses + CardInstallments;

    public bool Equals(CategorySpending? other)
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
               && DirectExpenses == other.DirectExpenses
               && FixedExpenses == other.FixedExpenses
               && CardInstallments == other.CardInstallments
               && Categories.SequenceEqual(other.Categories);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Month);
        hash.Add(DirectExpenses);
        hash.Add(FixedExpenses);
        hash.Add(CardInstallments);
        foreach (var category in Categories)
        {
            hash.Add(category);
        }

        return hash.ToHashCode();
    }
}
