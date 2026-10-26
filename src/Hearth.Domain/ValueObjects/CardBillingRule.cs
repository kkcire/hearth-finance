namespace Hearth.Domain.ValueObjects;

/// <summary>
/// Billing configuration of a credit card, defined by the user.
/// Always valid: it can only be created through <see cref="Of"/>.
/// </summary>
public sealed record CardBillingRule
{
    public const int MinClosingDay = 1;
    public const int MaxClosingDay = 28;
    public const int MinDueDay = 1;
    public const int MaxDueDay = 28;

    private CardBillingRule(int closingDay, int dueDay)
    {
        ClosingDay = closingDay;
        DueDay = dueDay;
    }

    /// <summary>
    /// Day of the month on which the invoice closes. Purchases made before this day
    /// belong to the invoice closing this month; purchases from this day on belong
    /// to the next one.
    /// </summary>
    public int ClosingDay { get; }

    /// <summary>
    /// Day of the month on which the invoice is due. It is the first occurrence of this day
    /// after the closing date, so a due day that is not after the closing day falls in the
    /// following month.
    /// </summary>
    public int DueDay { get; }

    /// <summary>
    /// Creates a billing rule. Both days must exist in every month of the year,
    /// so they are limited to 1 through 28.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A day is outside 1 to 28.</exception>
    public static CardBillingRule Of(int closingDay, int dueDay)
    {
        if (closingDay < MinClosingDay || closingDay > MaxClosingDay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(closingDay),
                closingDay,
                $"The closing day must be between {MinClosingDay} and {MaxClosingDay}.");
        }

        if (dueDay < MinDueDay || dueDay > MaxDueDay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dueDay),
                dueDay,
                $"The due day must be between {MinDueDay} and {MaxDueDay}.");
        }

        return new CardBillingRule(closingDay, dueDay);
    }
}