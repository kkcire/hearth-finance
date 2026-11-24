namespace Hearth.Domain.ValueObjects;

/// <summary>
/// The projected result of one month (accrual basis), with the components it comes from.
/// Created only by <c>ProjectedResultCalculator</c>.
/// </summary>
public sealed record ProjectedResult
{
    internal ProjectedResult(
        YearMonth month,
        Money incomes,
        Money cardInvoice,
        Money directExpenses,
        Money fixedExpenses,
        Guid? conditionId = null)
    {
        Month = month;
        Incomes = incomes;
        CardInvoice = cardInvoice;
        DirectExpenses = directExpenses;
        FixedExpenses = fixedExpenses;
        ConditionId = conditionId;
    }

    public YearMonth Month { get; }

    /// <summary>Optional purchase condition filter applied to <see cref="CardInvoice"/> (<see langword="null"/> = all conditions).</summary>
    public Guid? ConditionId { get; }

    /// <summary>Every income expected in the month (salary included), received or not.</summary>
    public Money Incomes { get; }

    /// <summary>The derived invoice of the cycle closing in the month.</summary>
    public Money CardInvoice { get; }

    /// <summary>The direct expenses dated in the month.</summary>
    public Money DirectExpenses { get; }

    /// <summary>The fixed expenses active in the month, paid or not, at the amount of the month.</summary>
    public Money FixedExpenses { get; }

    /// <summary>
    /// Incomes minus invoice, direct expenses and fixed expenses. It can be negative.
    /// Derived from the components, so it can never disagree with them.
    /// </summary>
    public Money Result => Incomes - CardInvoice - DirectExpenses - FixedExpenses;
}