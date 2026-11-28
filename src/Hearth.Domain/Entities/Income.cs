using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Entities;

/// <summary>
/// An income, such as a salary or a Pix transfer from a relative. Its status is purely derived
/// from its single <see cref="Date"/> relative to a reference date: dates on or before today count
/// as received (real balance / cash), while future dates remain expected (projected result / accrual).
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
        DateOnly date,
        Money amount,
        string description,
        Guid conditionId)
    {
        Id = id;
        AccountId = accountId;
        Date = date;
        Amount = amount;
        Description = description;
        ConditionId = conditionId;
    }

    public Guid Id { get; }

    /// <summary>The account this income belongs to. Prepares the model for multiple accounts.</summary>
    public Guid AccountId { get; }

    /// <summary>
    /// The canonical date of this income. Decides both the competence month and whether the income
    /// has already entered cash balance on a given reference date.
    /// </summary>
    public DateOnly Date { get; private set; }

    public Money Amount { get; private set; }

    public string Description { get; private set; }

    /// <summary>
    /// Attribution condition (e.g., "Própria" for personal income, or "Outros" and personalized conditions for
    /// third-party reimbursements/transfers). Defaults to <see cref="SystemPurchaseConditions.OwnId"/>.
    /// </summary>
    public Guid ConditionId { get; private set; }

    /// <summary>True when this income represents personal/own income rather than a third-party transfer or reimbursement.</summary>
    public bool IsOwn => ConditionId == SystemPurchaseConditions.OwnId;

    /// <summary>The calendar month this income counts toward.</summary>
    public YearMonth Month => YearMonth.From(Date);

    /// <summary>
    /// Pure time-derived receipt status: returns true when this income's <see cref="Date"/>
    /// is on or before <paramref name="referenceDate"/>.
    /// </summary>
    public bool IsReceivedOn(DateOnly referenceDate) => Date <= referenceDate;

    /// <summary>Creates an income enforcing the account's tracking start boundary.</summary>
    public static Income Create(
        Guid id,
        Account account,
        DateOnly date,
        Money amount,
        string description,
        Guid? conditionId = null)
    {
        ArgumentNullException.ThrowIfNull(account);
        account.EnsureCanRegisterOn(date);

        return Create(id, account.Id, date, amount, description, conditionId);
    }

    /// <summary>Creates an income with the given canonical date.</summary>
    public static Income Create(
        Guid id,
        Guid accountId,
        DateOnly date,
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

        return new Income(id, accountId, date, amount, validDescription, resolvedConditionId);
    }

    public void UpdateDate(Account account, DateOnly newDate)
    {
        ArgumentNullException.ThrowIfNull(account);
        account.EnsureCanRegisterOn(newDate);
        UpdateDate(newDate);
    }

    public void UpdateDate(DateOnly newDate)
    {
        Date = newDate;
    }

    public void UpdateAmount(Money newAmount)
    {
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
