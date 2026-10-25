using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Entities;

/// <summary>
/// An income, such as a salary or a Pix transfer from a relative. It is always created as
/// expected (counts in the projected result, by accrual) and can be marked as received
/// (counts in the real balance, by cash).
/// </summary>
public sealed class Income
{
    private Income(
        Guid id,
        Guid accountId,
        DateOnly expectedDate,
        Money amount,
        string description)
    {
        Id = id;
        AccountId = accountId;
        ExpectedDate = expectedDate;
        Amount = amount;
        Description = description;
    }

    public Guid Id { get; }

    /// <summary>The account this income belongs to. Prepares the model for multiple accounts.</summary>
    public Guid AccountId { get; }

    /// <summary>The date the income is expected. It decides the month in the projected result.</summary>
    public DateOnly ExpectedDate { get; private set; }

    public Money Amount { get; private set; }

    public string Description { get; private set; }

    /// <summary>
    /// The date the money actually arrived. It decides the month in the real balance.
    /// Null while the income is only expected.
    /// </summary>
    public DateOnly? ReceivedOn { get; private set; }

    /// <summary>Derived from <see cref="ReceivedOn"/>, so the two can never disagree.</summary>
    public bool IsReceived => ReceivedOn.HasValue;

    /// <summary>The month the income counts toward in the projected result (accrual).</summary>
    public YearMonth ExpectedMonth => YearMonth.From(ExpectedDate);

    /// <summary>The month the income counts toward in the real balance (cash), once received.</summary>
    public YearMonth? ReceivedMonth => ReceivedOn is { } date ? YearMonth.From(date) : null;

    /// <summary>Creates an income as expected. To register one that already happened, mark it as received.</summary>
    public static Income Create(
        Guid id,
        Guid accountId,
        DateOnly expectedDate,
        Money amount,
        string description)
    {
        ValidateId(id, nameof(id));
        ValidateId(accountId, nameof(accountId));
        ValidateAmount(amount, nameof(amount));
        var validDescription = ValidateDescription(description);

        return new Income(id, accountId, expectedDate, amount, validDescription);
    }

    /// <summary>
    /// Marks the income as received. Idempotent: if it is already received, nothing changes
    /// and the original date is kept, so repeating the operation never counts it twice.
    /// </summary>
    /// <returns>True if the state changed; false if it was already received.</returns>
    public bool MarkAsReceived(DateOnly receivedOn)
    {
        if (IsReceived)
        {
            return false;
        }

        ReceivedOn = receivedOn;
        return true;
    }

    /// <summary>
    /// Takes the income back to expected, to fix a wrong mark. Idempotent: if it is not
    /// received, nothing changes.
    /// </summary>
    /// <returns>True if the state changed; false if it was already expected.</returns>
    public bool RevertToExpected()
    {
        if (!IsReceived)
        {
            return false;
        }

        ReceivedOn = null;
        return true;
    }

    public void UpdateAmount(Money newAmount)
    {
        ValidateAmount(newAmount, nameof(newAmount));

        Amount = newAmount;
    }

    public void UpdateDescription(string description)
    {
        Description = ValidateDescription(description);
    }

    private static void ValidateId(Guid id, string paramName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The id must not be empty.", paramName);
        }
    }

    private static void ValidateAmount(Money amount, string paramName)
    {
        if (amount <= Money.Zero)
        {
            throw new ArgumentOutOfRangeException(
                paramName, amount, "The income amount must be greater than zero.");
        }
    }

    private static string ValidateDescription(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return description.Trim();
    }
}