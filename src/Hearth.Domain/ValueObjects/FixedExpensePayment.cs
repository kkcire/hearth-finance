namespace Hearth.Domain.ValueObjects;

/// <summary>
/// One recorded monthly payment of a fixed expense: the competence month that was paid and the
/// calendar date the payment occurred. Created only by the fixed expense that owns it.
/// </summary>
public sealed record FixedExpensePayment
{
    private FixedExpensePayment()
    {
        Month = null!;
    }

    internal FixedExpensePayment(YearMonth month, DateOnly paidOn)
    {
        Month = month;
        PaidOn = paidOn;
    }

    public YearMonth Month { get; }

    public DateOnly PaidOn { get; }
}
