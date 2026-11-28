namespace Hearth.Domain.ValueObjects;

/// <summary>
/// One recorded monthly receipt of a fixed income: the competence month that was received,
/// the calendar date the money arrived, and the actual amount received (allowing bonuses,
/// commissions, or deductions without mutating the baseline schedule).
/// </summary>
public sealed record FixedIncomeReceipt
{
    private FixedIncomeReceipt()
    {
        Month = null!;
    }

    internal FixedIncomeReceipt(YearMonth month, DateOnly receivedOn, Money actualAmount)
    {
        Month = month;
        ReceivedOn = receivedOn;
        ActualAmount = actualAmount;
    }

    public YearMonth Month { get; }

    public DateOnly ReceivedOn { get; }

    public Money ActualAmount { get; }
}
