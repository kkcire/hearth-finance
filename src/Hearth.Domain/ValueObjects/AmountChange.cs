namespace Hearth.Domain.ValueObjects;

/// <summary>
/// One entry in the amount history of a recurring monthly schedule (fixed expense or category
/// budget): the amount that applies from the given month until the next entry.
/// </summary>
public sealed record AmountChange
{
    private AmountChange()
    {
        EffectiveFrom = null!;
    }

    internal AmountChange(YearMonth effectiveFrom, Money amount)
    {
        EffectiveFrom = effectiveFrom;
        Amount = amount;
    }

    public YearMonth EffectiveFrom { get; }

    public Money Amount { get; }
}