namespace Hearth.Domain.ValueObjects;

/// <summary>
/// Cash-basis monthly surplus projection (or historical closing) for a single calendar month,
/// structured as a cascade from starting cash balance through layered commitments.
/// Created only by <c>MonthlySurplusCalculator</c>.
/// </summary>
public sealed record MonthlySurplus
{
    internal MonthlySurplus(
        YearMonth month,
        bool isHistorical,
        bool isCurrentMonth,
        Money startingCashBalance,
        Money expectedIncomes,
        Money cardInvoiceDue,
        Money fixedExpenses,
        Money directExpenses,
        Money netMonthlyVariation)
    {
        Month = month;
        IsHistorical = isHistorical;
        IsCurrentMonth = isCurrentMonth;
        StartingCashBalance = startingCashBalance;
        ExpectedIncomes = expectedIncomes;
        CardInvoiceDue = cardInvoiceDue;
        FixedExpenses = fixedExpenses;
        DirectExpenses = directExpenses;
        NetMonthlyVariation = netMonthlyVariation;
    }

    public YearMonth Month { get; }

    /// <summary>True when <see cref="Month"/> is strictly before the reference month.</summary>
    public bool IsHistorical { get; }

    /// <summary>True when <see cref="Month"/> is the reference month.</summary>
    public bool IsCurrentMonth { get; }

    /// <summary>
    /// Starting cash balance for the cascade: real cash balance on <c>today</c> for the current month,
    /// previous month's projected surplus rollover for future months, and opening cash balance for past months.
    /// </summary>
    public Money StartingCashBalance { get; }

    /// <summary>
    /// Expected incomes not yet settled on or before <c>today</c> (for current/future months),
    /// or actual received incomes in the month (for past months).
    /// </summary>
    public Money ExpectedIncomes { get; }

    /// <summary>Total available cash: <see cref="StartingCashBalance"/> + <see cref="ExpectedIncomes"/>.</summary>
    public Money TotalAvailable => StartingCashBalance + ExpectedIncomes;

    /// <summary>
    /// Active credit card installments (including archived cards) whose due date falls in <see cref="Month"/>
    /// and is after <c>today</c> (or settled during <see cref="Month"/> for past months).
    /// </summary>
    public Money CardInvoiceDue { get; }

    /// <summary>
    /// Active fixed expenses in <see cref="Month"/> not yet paid on or before <c>today</c>
    /// (or paid during <see cref="Month"/> for past months).
    /// </summary>
    public Money FixedExpenses { get; }

    /// <summary>
    /// Direct expenses in <see cref="Month"/> dated after <c>today</c>
    /// (or dated during <see cref="Month"/> for past months).
    /// </summary>
    public Money DirectExpenses { get; }

    /// <summary>Sum of all discount layers: card invoice, fixed expenses, and direct expenses.</summary>
    public Money TotalDiscounts => CardInvoiceDue + FixedExpenses + DirectExpenses;

    /// <summary>
    /// Projected closing cash surplus for <see cref="Month"/>: <see cref="TotalAvailable"/> − <see cref="TotalDiscounts"/>.
    /// For historical months this equals the real cash balance on the last day of <see cref="Month"/>.
    /// </summary>
    public Money ProjectedSurplus => TotalAvailable - TotalDiscounts;

    /// <summary>
    /// Net cash variation generated during <see cref="Month"/> (closing surplus minus the month's opening cash).
    /// Unlike <see cref="ProjectedSurplus"/>, this measures the monthly flow rather than the accumulated balance.
    /// </summary>
    public Money NetMonthlyVariation { get; }
}
