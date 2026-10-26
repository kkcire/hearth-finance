namespace Hearth.Domain.ValueObjects;

/// <summary>
/// One invoice cycle of a credit card, identified by the month in which it closes.
/// The cycle closing in month M covers the purchases from the closing day of month M-1
/// up to the day before the closing day of month M (both inclusive).
/// Always valid: it can only be created through <see cref="Closing"/> or <see cref="ForPurchase"/>.
/// </summary>
public sealed record BillingCycle
{
    private BillingCycle(YearMonth closingMonth, CardBillingRule rule)
    {
        ClosingMonth = closingMonth;
        Rule = rule;
    }

    public YearMonth ClosingMonth { get; }

    public CardBillingRule Rule { get; }

    /// <summary>The day the invoice closes. Purchases made on this day already belong to the next cycle.</summary>
    public DateOnly ClosingDate => new(ClosingMonth.Year, ClosingMonth.Month, Rule.ClosingDay);

    /// <summary>The first day covered by this cycle: the closing day of the previous month.</summary>
    public DateOnly StartDate
    {
        get
        {
            var previous = ClosingMonth.AddMonths(-1);
            return new DateOnly(previous.Year, previous.Month, Rule.ClosingDay);
        }
    }

    /// <summary>The last day covered by this cycle: the day before the closing date.</summary>
    public DateOnly EndDate => ClosingDate.AddDays(-1);

    /// <summary>
    /// The date the invoice is due: the first occurrence of the due day after the closing date.
    /// It is in the closing month when the due day comes after the closing day, and in the
    /// following month otherwise (including when both days are equal).
    /// </summary>
    public DateOnly DueDate
    {
        get
        {
            var dueMonth = Rule.DueDay > Rule.ClosingDay ? ClosingMonth : ClosingMonth.AddMonths(1);
            return new DateOnly(dueMonth.Year, dueMonth.Month, Rule.DueDay);
        }
    }

    /// <summary>Returns the cycle that closes in the given month.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The month is the first supported month, which has no previous month to start the cycle in,
    /// or the last supported month with a due day that would fall in the year after it.
    /// </exception>
    public static BillingCycle Closing(YearMonth closingMonth, CardBillingRule rule)
    {
        ArgumentNullException.ThrowIfNull(closingMonth);
        ArgumentNullException.ThrowIfNull(rule);

        if (closingMonth.Year == YearMonth.MinYear && closingMonth.Month == 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(closingMonth),
                closingMonth,
                "A cycle needs a previous month to start in, so the first supported month cannot be a closing month.");
        }

        var isLastSupportedMonth = closingMonth.Year == YearMonth.MaxYear && closingMonth.Month == 12;
        if (isLastSupportedMonth && rule.DueDay <= rule.ClosingDay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(closingMonth),
                closingMonth,
                "The due date of the last supported month would fall outside the supported years.");
        }

        return new BillingCycle(closingMonth, rule);
    }

    /// <summary>
    /// Returns the cycle to which a purchase made on the given date belongs. A purchase made
    /// before the closing day goes to the cycle closing in the same month; from the closing
    /// day on, it goes to the cycle closing in the next month.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The resulting cycle is outside the supported years.</exception>
    public static BillingCycle ForPurchase(DateOnly purchaseDate, CardBillingRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var purchaseMonth = YearMonth.From(purchaseDate);
        var closingMonth = purchaseDate.Day < rule.ClosingDay
            ? purchaseMonth
            : purchaseMonth.AddMonths(1);

        return Closing(closingMonth, rule);
    }

    /// <summary>
    /// Moves forward (or backward, with a negative value) by the given number of cycles.
    /// The installment k of a purchase belongs to <c>firstCycle.AddCycles(k - 1)</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The result is outside the supported range.</exception>
    public BillingCycle AddCycles(int cycles) => Closing(ClosingMonth.AddMonths(cycles), Rule);
}