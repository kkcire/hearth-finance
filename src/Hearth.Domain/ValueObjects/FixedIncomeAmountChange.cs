namespace Hearth.Domain.ValueObjects;

/// <summary>
/// One entry in the expected amount history of a fixed income: the baseline amount that applies
/// from the given month until the next entry. Created only by the fixed income that owns it.
/// </summary>
public sealed record FixedIncomeAmountChange
{
    private FixedIncomeAmountChange()
    {
        EffectiveFrom = null!;
    }

    internal FixedIncomeAmountChange(YearMonth effectiveFrom, Money amount)
    {
        EffectiveFrom = effectiveFrom;
        Amount = amount;
    }

    public YearMonth EffectiveFrom { get; }

    public Money Amount { get; }
}
