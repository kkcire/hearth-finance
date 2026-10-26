using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Entities;

/// <summary>
/// A bank account with its credit card configuration. It holds the starting point of the real
/// balance (initial balance and date) and the user's billing rule. Entries (purchases, expenses,
/// incomes) are separate aggregates that point to the account by id.
/// </summary>
public sealed class Account
{
    private Account(
        Guid id,
        string name,
        Money initialBalance,
        DateOnly initialBalanceDate,
        CardBillingRule billingRule)
    {
        Id = id;
        Name = name;
        InitialBalance = initialBalance;
        InitialBalanceDate = initialBalanceDate;
        BillingRule = billingRule;
    }

    public Guid Id { get; }

    public string Name { get; private set; }

    /// <summary>The money available on <see cref="InitialBalanceDate"/>. It can be zero or negative.</summary>
    public Money InitialBalance { get; private set; }

    /// <summary>
    /// The date the initial balance refers to. No entry can be registered before it, except the
    /// earlier installments of purchases already in progress. It cannot be changed after creation.
    /// </summary>
    public DateOnly InitialBalanceDate { get; }

    /// <summary>
    /// The user's card configuration. Its closing day also decides when fixed expenses freeze.
    /// </summary>
    public CardBillingRule BillingRule { get; private set; }

    public static Account Create(
        Guid id,
        string name,
        Money initialBalance,
        DateOnly initialBalanceDate,
        CardBillingRule billingRule)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The id must not be empty.", nameof(id));
        }

        var validName = ValidateName(name);
        ArgumentNullException.ThrowIfNull(billingRule);

        return new Account(id, validName, initialBalance, initialBalanceDate, billingRule);
    }

    /// <summary>True if an entry dated on the given day respects the initial balance date.</summary>
    public bool CanRegisterOn(DateOnly date) => date >= InitialBalanceDate;

    /// <exception cref="InvalidOperationException">The date is before the initial balance date.</exception>
    public void EnsureCanRegisterOn(DateOnly date)
    {
        if (!CanRegisterOn(date))
        {
            throw new InvalidOperationException(
                $"No entry can be registered before the initial balance date ({InitialBalanceDate:yyyy-MM-dd}).");
        }
    }

    public void Rename(string name)
    {
        Name = ValidateName(name);
    }

    /// <summary>
    /// Corrects the initial balance amount. The date is kept, so no registered entry becomes invalid.
    /// </summary>
    public void UpdateInitialBalance(Money newInitialBalance)
    {
        InitialBalance = newInitialBalance;
    }

    /// <summary>
    /// Replaces the card configuration. Purchases already registered keep the rule they were
    /// created with; fixed expenses and later purchases follow the new one.
    /// </summary>
    public void ChangeBillingRule(CardBillingRule billingRule)
    {
        ArgumentNullException.ThrowIfNull(billingRule);

        BillingRule = billingRule;
    }

    private static string ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return name.Trim();
    }
}