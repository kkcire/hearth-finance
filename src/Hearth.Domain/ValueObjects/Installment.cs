namespace Hearth.Domain.ValueObjects;

/// <summary>
/// One installment of a credit card purchase. Created only by the purchase that owns it.
/// </summary>
public sealed record Installment
{
    internal Installment(int number, Money amount, BillingCycle cycle, bool isPreviouslyPaid)
    {
        Number = number;
        Amount = amount;
        Cycle = cycle;
        IsPreviouslyPaid = isPreviouslyPaid;
    }

    /// <summary>The installment number, starting at 1.</summary>
    public int Number { get; }

    public Money Amount { get; }

    /// <summary>The invoice cycle this installment belongs to.</summary>
    public BillingCycle Cycle { get; }

    /// <summary>
    /// True for installments before the initial one of a purchase already in progress.
    /// They are considered paid: they generate no invoice and do not affect the real balance.
    /// </summary>
    public bool IsPreviouslyPaid { get; }
}