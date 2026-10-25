namespace Hearth.Domain.ValueObjects;

/// <summary>
/// One entry in the amount history of a fixed expense: the amount that applies from the
/// given month until the next entry. Created only by the fixed expense that owns it.
/// </summary>
public sealed record AmountChange
{
    internal AmountChange(YearMonth effectiveFrom, Money amount)
    {
        EffectiveFrom = effectiveFrom;
        Amount = amount;
    }

    public YearMonth EffectiveFrom { get; }

    public Money Amount { get; }
}