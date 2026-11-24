using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Entities;

/// <summary>
/// A financial account holding the starting point of the cash balance (initial balance and
/// reference date). Entries (incomes, direct expenses, fixed expenses, credit card purchases)
/// and credit cards point to the account by id.
/// </summary>
public sealed class Account
{
    private Account()
    {
        Name = null!;
    }

    private Account(
        Guid id,
        Guid ownerUserId,
        string name,
        Money initialBalance,
        DateOnly initialBalanceDate,
        bool isInitialBalanceConfirmed)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        Name = name;
        InitialBalance = initialBalance;
        InitialBalanceDate = initialBalanceDate;
        IsInitialBalanceConfirmed = isInitialBalanceConfirmed;
    }

    public Guid Id { get; }

    /// <summary>The user who owns this account. Kept as OwnerUserId to accommodate future shared accounts.</summary>
    public Guid OwnerUserId { get; }

    public string Name { get; private set; }

    /// <summary>The money available on <see cref="InitialBalanceDate"/>. It can be zero or negative.</summary>
    public Money InitialBalance { get; private set; }

    /// <summary>
    /// The account creation date (reference date of the initial balance). Direct cash movements
    /// cannot be registered before it; credit card purchases can be registered freely for invoice
    /// history while installments with due dates before it are ignored by cash, limit and surplus calculations.
    /// Always immutable.
    /// </summary>
    public DateOnly InitialBalanceDate { get; }

    /// <summary>
    /// True once the user has either confirmed or explicitly skipped the initial balance setup.
    /// Prevents the first-access modal from reappearing even when the chosen initial balance is zero.
    /// </summary>
    public bool IsInitialBalanceConfirmed { get; private set; }

    public static Account Create(
        Guid id,
        Guid ownerUserId,
        string name,
        Money initialBalance,
        DateOnly initialBalanceDate,
        bool isInitialBalanceConfirmed = false)
    {
        DomainValidation.EnsureNotEmpty(id, nameof(id));
        DomainValidation.EnsureNotEmpty(ownerUserId, nameof(ownerUserId), "owner user id");
        var validName = DomainValidation.ValidateRequiredText(name, nameof(name), DomainValidation.MaxNameLength);

        return new Account(
            id,
            ownerUserId,
            validName,
            initialBalance,
            initialBalanceDate,
            isInitialBalanceConfirmed);
    }

    /// <summary>True if an entry dated on the given day respects the initial balance date.</summary>
    public bool CanRegisterOn(DateOnly date) => date >= InitialBalanceDate;

    /// <exception cref="DomainException">The date is before the initial balance date.</exception>
    public void EnsureCanRegisterOn(DateOnly date)
    {
        if (!CanRegisterOn(date))
        {
            throw new DomainException(
                $"No entry can be registered before the initial balance date ({InitialBalanceDate:yyyy-MM-dd}).");
        }
    }

    /// <summary>
    /// Completes the one-time initial balance setup using the account's creation date.
    /// </summary>
    /// <exception cref="DomainException">The initial balance was already confirmed.</exception>
    public void ConfirmInitialBalance(Money initialBalance)
    {
        if (IsInitialBalanceConfirmed)
        {
            throw new DomainException(
                "The initial balance has already been confirmed. Use UpdateInitialBalance to change the amount.");
        }

        InitialBalance = initialBalance;
        IsInitialBalanceConfirmed = true;
    }

    /// <summary>
    /// Skips the initial balance setup by recording zero starting from the account's creation date.
    /// </summary>
    public void SkipInitialBalance()
    {
        ConfirmInitialBalance(Money.Zero);
    }

    public void Rename(string name)
    {
        Name = DomainValidation.ValidateRequiredText(name, nameof(name), DomainValidation.MaxNameLength);
    }

    /// <summary>
    /// Corrects the initial balance amount. The date is kept, so no registered entry becomes invalid.
    /// Requires the initial balance to have been confirmed or skipped first.
    /// </summary>
    /// <exception cref="DomainException">The initial balance has not been confirmed or skipped yet.</exception>
    public void UpdateInitialBalance(Money newInitialBalance)
    {
        if (!IsInitialBalanceConfirmed)
        {
            throw new DomainException(
                "The initial balance must be confirmed or skipped before it can be updated.");
        }

        InitialBalance = newInitialBalance;
    }
}
