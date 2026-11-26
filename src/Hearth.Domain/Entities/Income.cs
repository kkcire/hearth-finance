using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Entities;

/// <summary>
/// An income, such as a salary or a Pix transfer from a relative. It is always created as
/// expected (counts in the projected result, by accrual) and can be marked as received
/// (counts in the real balance, by cash).
/// </summary>
public sealed class Income
{
    private Income()
    {
        Description = null!;
    }

    private Income(
        Guid id,
        Guid accountId,
        DateOnly expectedDate,
        Money amount,
        string description,
        Guid conditionId)
    {
        Id = id;
        AccountId = accountId;
        ExpectedDate = expectedDate;
        Amount = amount;
        Description = description;
        ConditionId = conditionId;
    }

    public Guid Id { get; }

    /// <summary>The account this income belongs to. Prepares the model for multiple accounts.</summary>
    public Guid AccountId { get; }

    /// <summary>The date the income is expected. It decides the month in the projected result.</summary>
    public DateOnly ExpectedDate { get; private set; }

    public Money Amount { get; private set; }

    public string Description { get; private set; }

    /// <summary>
    /// Attribution condition (e.g., "Própria" for personal income, or "Pai"/"Mãe"/"Outros" for
    /// third-party reimbursements/transfers). Defaults to <see cref="SystemPurchaseConditions.OwnId"/>.
    /// </summary>
    public Guid ConditionId { get; private set; }

    /// <summary>True when this income represents personal/own income rather than a third-party transfer or reimbursement.</summary>
    public bool IsOwn => ConditionId == SystemPurchaseConditions.OwnId;

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

    /// <summary>Creates an income enforcing the account's tracking start boundary.</summary>
    public static Income Create(
        Guid id,
        Account account,
        DateOnly expectedDate,
        Money amount,
        string description,
        Guid? conditionId = null)
    {
        ArgumentNullException.ThrowIfNull(account);
        account.EnsureCanRegisterOn(expectedDate);

        return Create(id, account.Id, expectedDate, amount, description, conditionId);
    }

    /// <summary>Creates an income as expected. To register one that already happened, mark it as received.</summary>
    public static Income Create(
        Guid id,
        Guid accountId,
        DateOnly expectedDate,
        Money amount,
        string description,
        Guid? conditionId = null)
    {
        DomainValidation.EnsureNotEmpty(id, nameof(id));
        DomainValidation.EnsureNotEmpty(accountId, nameof(accountId), "account id");
        DomainValidation.EnsurePositiveAmount(amount, nameof(amount), "income amount");
        var validDescription = DomainValidation.ValidateRequiredText(
            description, nameof(description), DomainValidation.MaxDescriptionLength);

        var resolvedConditionId = conditionId ?? SystemPurchaseConditions.OwnId;
        DomainValidation.EnsureNotEmpty(resolvedConditionId, nameof(conditionId), "income condition id");

        return new Income(id, accountId, expectedDate, amount, validDescription, resolvedConditionId);
    }

    public bool MarkAsReceived(Account account, DateOnly receivedOn, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(account);
        account.EnsureCanRegisterOn(receivedOn);
        return MarkAsReceived(receivedOn, today);
    }

    /// <summary>
    /// Marks the income as received. Idempotent: if it is already received, nothing changes
    /// and the original date is kept, so repeating the operation never counts it twice.
    /// </summary>
    /// <returns>True if the state changed; false if it was already received.</returns>
    /// <exception cref="DomainException">The received date is in the future relative to <paramref name="today"/>.</exception>
    public bool MarkAsReceived(DateOnly receivedOn, DateOnly today)
    {
        if (receivedOn > today)
        {
            throw new DomainException("An income cannot be marked as received on a future date.");
        }

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

    public void UpdateExpectedDate(Account account, DateOnly newExpectedDate)
    {
        ArgumentNullException.ThrowIfNull(account);
        account.EnsureCanRegisterOn(newExpectedDate);
        UpdateExpectedDate(newExpectedDate);
    }

    /// <exception cref="DomainException">The income is already marked as received.</exception>
    public void UpdateExpectedDate(DateOnly newExpectedDate)
    {
        if (IsReceived)
        {
            throw new DomainException(
                "The expected date of a received income cannot be changed. Revert it to expected first.");
        }

        ExpectedDate = newExpectedDate;
    }

    /// <exception cref="DomainException">The income is already marked as received.</exception>
    public void UpdateAmount(Money newAmount)
    {
        if (IsReceived)
        {
            throw new DomainException(
                "The amount of a received income cannot be changed. Revert it to expected first.");
        }

        DomainValidation.EnsurePositiveAmount(newAmount, nameof(newAmount), "income amount");

        Amount = newAmount;
    }

    public void UpdateDescription(string description)
    {
        Description = DomainValidation.ValidateRequiredText(
            description, nameof(description), DomainValidation.MaxDescriptionLength);
    }

    public void UpdateCondition(Guid conditionId)
    {
        DomainValidation.EnsureNotEmpty(conditionId, nameof(conditionId), "income condition id");

        ConditionId = conditionId;
    }
}