namespace Hearth.Domain.ValueObjects;

/// <summary>
/// The spending of one category in a month (accrual basis), broken down by source.
/// Created only by <c>CategorySpendingCalculator</c>.
/// </summary>
public sealed record CategorySpendingItem
{
    internal CategorySpendingItem(
        Guid categoryId,
        Money directExpenses,
        Money fixedExpenses,
        Money cardInstallments)
    {
        CategoryId = categoryId;
        DirectExpenses = directExpenses;
        FixedExpenses = fixedExpenses;
        CardInstallments = cardInstallments;
    }

    public Guid CategoryId { get; }

    /// <summary>Direct expenses dated in the month for this category.</summary>
    public Money DirectExpenses { get; }

    /// <summary>Fixed expenses active in the month for this category, at the amount of the month.</summary>
    public Money FixedExpenses { get; }

    /// <summary>Card installments (excluding previously paid ones) in the cycle closing in the month.</summary>
    public Money CardInstallments { get; }

    /// <summary>
    /// Total spending in the category. Derived from the components, so it can never disagree with them.
    /// </summary>
    public Money Total => DirectExpenses + FixedExpenses + CardInstallments;
}
