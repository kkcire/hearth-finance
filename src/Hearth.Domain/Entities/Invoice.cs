using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Entities;

/// <summary>
/// The record of what the bank actually charged for one invoice cycle of an account, and of when
/// it was paid. The calculated invoice (the sum of the installments of the cycle) is never stored
/// here: the projected result uses the calculated amount, and the real balance uses the charged one.
/// </summary>
public sealed class Invoice
{
    private Invoice(Guid id, Guid accountId, BillingCycle cycle)
    {
        Id = id;
        AccountId = accountId;
        Cycle = cycle;
    }

    public Guid Id { get; }

    /// <summary>The account this invoice belongs to. Prepares the model for multiple accounts.</summary>
    public Guid AccountId { get; }

    /// <summary>
    /// The cycle this invoice covers. It keeps the rule it was created with, so changing the
    /// closing day of the card later never alters invoices that already exist.
    /// </summary>
    public BillingCycle Cycle { get; }

    /// <summary>The amount the bank charged. Null until it is informed.</summary>
    public Money? ChargedAmount { get; private set; }

    /// <summary>
    /// The date the invoice was paid. It decides the month in the real balance (cash).
    /// Null while the invoice is open.
    /// </summary>
    public DateOnly? PaidOn { get; private set; }

    /// <summary>Derived from <see cref="PaidOn"/>, so the two can never disagree.</summary>
    public bool IsPaid => PaidOn.HasValue;

    /// <summary>The month the payment counts toward in the real balance, once paid.</summary>
    public YearMonth? PaidMonth => PaidOn is { } date ? YearMonth.From(date) : null;

    /// <summary>Creates an open invoice, with no charged amount yet.</summary>
    public static Invoice Create(Guid id, Guid accountId, BillingCycle cycle)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The id must not be empty.", nameof(id));
        }

        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("The account id must not be empty.", nameof(accountId));
        }

        ArgumentNullException.ThrowIfNull(cycle);

        return new Invoice(id, accountId, cycle);
    }

    /// <summary>
    /// Informs or corrects the amount the bank charged. Allowed only while the invoice is not paid.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    /// <exception cref="InvalidOperationException">The invoice is already paid.</exception>
    public void InformChargedAmount(Money amount)
    {
        if (amount < Money.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount), amount, "The charged amount must not be negative.");
        }

        if (IsPaid)
        {
            throw new InvalidOperationException(
                "The charged amount cannot be changed on a paid invoice. Revert the payment first.");
        }

        ChargedAmount = amount;
    }

    /// <summary>
    /// Marks the invoice as paid. Idempotent: if it is already paid, nothing changes and the
    /// original date is kept, so repeating the operation never counts it twice.
    /// </summary>
    /// <returns>True if the state changed; false if the invoice was already paid.</returns>
    /// <exception cref="InvalidOperationException">The charged amount was not informed yet.</exception>
    public bool MarkAsPaid(DateOnly paidOn)
    {
        if (IsPaid)
        {
            return false;
        }

        if (ChargedAmount is null)
        {
            throw new InvalidOperationException(
                "The charged amount must be informed before the invoice can be paid.");
        }

        PaidOn = paidOn;
        return true;
    }

    /// <summary>
    /// Takes the payment back, to fix a wrong mark. Idempotent: if the invoice is not paid,
    /// nothing changes. The charged amount is kept.
    /// </summary>
    /// <returns>True if the state changed; false if the invoice was not paid.</returns>
    public bool RevertPayment()
    {
        if (!IsPaid)
        {
            return false;
        }

        PaidOn = null;
        return true;
    }

    /// <summary>
    /// The difference between what the bank charged and the amount calculated from the installments.
    /// Positive means the bank charged more; negative, less; zero means no divergence. Null while the
    /// charged amount is not informed. It is only information: no adjustment entry is ever created.
    /// </summary>
    public Money? DivergenceFrom(Money calculatedAmount) =>
        ChargedAmount is { } charged ? charged - calculatedAmount : null;
}