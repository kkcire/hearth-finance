using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Entities;

/// <summary>
/// An optional credit card linked to an account. Holds the billing rule (closing and due days)
/// and optional nickname and spending limit. Purchases snapshot the card's billing rule at
/// registration time so editing or archiving the card never alters historical invoices.
/// </summary>
public sealed class CreditCard
{
    private CreditCard()
    {
        BillingRule = null!;
    }

    private CreditCard(
        Guid id,
        Guid accountId,
        CardBillingRule billingRule,
        string? name,
        Money? limit)
    {
        Id = id;
        AccountId = accountId;
        BillingRule = billingRule;
        Name = name;
        Limit = limit;
    }

    public Guid Id { get; }

    public Guid AccountId { get; }

    public string? Name { get; private set; }

    public CardBillingRule BillingRule { get; private set; }

    public Money? Limit { get; private set; }

    public bool IsArchived { get; private set; }

    public static CreditCard Create(
        Guid id,
        Guid accountId,
        CardBillingRule billingRule,
        string? name = null,
        Money? limit = null)
    {
        DomainValidation.EnsureNotEmpty(id, nameof(id));
        DomainValidation.EnsureNotEmpty(accountId, nameof(accountId), "account id");
        ArgumentNullException.ThrowIfNull(billingRule);

        var normalizedName = DomainValidation.ValidateOptionalText(name, nameof(name), DomainValidation.MaxNameLength);
        ValidateOptionalLimit(limit);

        return new CreditCard(id, accountId, billingRule, normalizedName, limit);
    }

    /// <summary>
    /// Updates the card's configuration. Already registered purchases keep the billing rule
    /// snapshot they were created with.
    /// </summary>
    public void UpdateDetails(CardBillingRule billingRule, string? name, Money? limit)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(billingRule);

        var normalizedName = DomainValidation.ValidateOptionalText(name, nameof(name), DomainValidation.MaxNameLength);
        ValidateOptionalLimit(limit);

        BillingRule = billingRule;
        Name = normalizedName;
        Limit = limit;
    }

    /// <summary>
    /// Archives the card so it can no longer receive new purchases while keeping historical
    /// installments, invoices, and balance calculations intact. Idempotent.
    /// </summary>
    public bool Archive()
    {
        if (IsArchived)
        {
            return false;
        }

        IsArchived = true;
        return true;
    }

    public void EnsureActive()
    {
        if (IsArchived)
        {
            throw new DomainException("An archived credit card cannot be used or modified.");
        }
    }

    private static void ValidateOptionalLimit(Money? limit)
    {
        if (limit.HasValue)
        {
            DomainValidation.EnsurePositiveAmount(limit.Value, nameof(limit), "credit limit");
        }
    }
}
