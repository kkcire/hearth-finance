namespace Hearth.Domain.ValueObjects;

/// <summary>
/// Billing configuration of a credit card, defined by the user.
/// Always valid: it can only be created through <see cref="Of"/>.
/// </summary>
public sealed record CardBillingRule
{
    public const int MinClosingDay = 1;
    public const int MaxClosingDay = 28;

    private CardBillingRule(int closingDay)
    {
        ClosingDay = closingDay;
    }

    /// <summary>
    /// Day of the month on which the invoice closes. Purchases made before this day
    /// belong to the invoice closing this month; purchases from this day on belong
    /// to the next one.
    /// </summary>
    public int ClosingDay { get; }

    /// <summary>
    /// Creates a billing rule. The closing day must exist in every month of the year,
    /// so it is limited to 1 through 28.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The closing day is outside 1 to 28.</exception>
    public static CardBillingRule Of(int closingDay)
    {
        if (closingDay < MinClosingDay || closingDay > MaxClosingDay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(closingDay),
                closingDay,
                $"The closing day must be between {MinClosingDay} and {MaxClosingDay}.");
        }

        return new CardBillingRule(closingDay);
    }
}