namespace Hearth.Domain.ValueObjects;

/// <summary>
/// The real balance of an account on a given day (cash basis), with the components it comes from.
/// Created only by <c>RealBalanceCalculator</c>.
/// </summary>
public sealed record RealBalance
{
    internal RealBalance(
        DateOnly until,
        Money initialBalance,
        Money incomes,
        Money directExpenses,
        Money fixedExpenses,
        Money cardInvoices)
    {
        Until = until;
        InitialBalance = initialBalance;
        Incomes = incomes;
        DirectExpenses = directExpenses;
        FixedExpenses = fixedExpenses;
        CardInvoices = cardInvoices;
    }

    /// <summary>The last day included in the balance, inclusive.</summary>
    public DateOnly Until { get; }

    /// <summary>The account's initial balance, the starting point of the cash flow.</summary>
    public Money InitialBalance { get; }

    /// <summary>Incomes received on or before <see cref="Until"/>, by their received date.</summary>
    public Money Incomes { get; }

    /// <summary>Direct expenses dated on or before <see cref="Until"/>.</summary>
    public Money DirectExpenses { get; }

    /// <summary>Fixed expenses paid on or before <see cref="Until"/>, at the amount of the month paid.</summary>
    public Money FixedExpenses { get; }

    /// <summary>Card installments whose due date is on or before <see cref="Until"/>.</summary>
    public Money CardInvoices { get; }

    /// <summary>
    /// Initial balance + incomes − direct expenses − fixed expenses − card invoices.
    /// Derived from the components, so it can never disagree with them. It can be negative.
    /// </summary>
    public Money Balance => InitialBalance + Incomes - DirectExpenses - FixedExpenses - CardInvoices;
}