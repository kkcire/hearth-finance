using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Entities;

/// <summary>
/// A financial account holding the immutable starting point of cash tracking (<see cref="InitialBalance"/>,
/// <see cref="CreatedAt"/>, and <see cref="TrackingStartDate"/> always fixed to day 1 of the creation month
/// in Brasília time) plus cash-only <see cref="BalanceAdjustments"/> for post-creation reconciliation.
/// </summary>
public sealed class Account
{
    private readonly List<BalanceAdjustment> _balanceAdjustments = [];

    private Account()
    {
        Name = null!;
    }

    private Account(
        Guid id,
        Guid ownerUserId,
        string name,
        Money initialBalance,
        DateTimeOffset createdAt,
        DateOnly trackingStartDate,
        bool isInitialBalanceConfirmed)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        Name = name;
        InitialBalance = initialBalance;
        CreatedAt = createdAt;
        TrackingStartDate = trackingStartDate;
        IsInitialBalanceConfirmed = isInitialBalanceConfirmed;
    }

    public Guid Id { get; }

    /// <summary>The user who owns this account. Kept as OwnerUserId to accommodate future shared accounts.</summary>
    public Guid OwnerUserId { get; }

    public string Name { get; private set; }

    /// <summary>
    /// The opening cash balance on <see cref="TrackingStartDate"/> (Day 1 of the entry month;
    /// may be negative, zero, or positive). This is the single manual balance of the account
    /// and can be updated via <see cref="SetBalance"/> without affecting competence metrics.
    /// </summary>
    public Money InitialBalance { get; private set; }

    /// <summary>UTC timestamp when the account was created.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Mandatory tracking start date, always fixed to the 1st day of the calendar month of
    /// <see cref="CreatedAt"/> evaluated in Brasília time (<c>America/Sao_Paulo</c>, UTC-3).
    /// Always immutable.
    /// </summary>
    public DateOnly TrackingStartDate { get; }

    /// <summary>The first calendar month tracked by this account.</summary>
    public YearMonth TrackingStartPeriod => YearMonth.From(TrackingStartDate);

    /// <summary>Alias for <see cref="TrackingStartDate"/> preserving compatibility with existing cash boundary references.</summary>
    public DateOnly InitialBalanceDate => TrackingStartDate;

    /// <summary>
    /// True once the initial balance has been set or explicitly confirmed/skipped during onboarding.
    /// </summary>
    public bool IsInitialBalanceConfirmed { get; private set; }

    /// <summary>
    /// Cash-only balance adjustments recorded on this account, ordered chronologically.
    /// </summary>
    public IReadOnlyList<BalanceAdjustment> BalanceAdjustments =>
        _balanceAdjustments
            .OrderBy(adjustment => adjustment.Date)
            .ThenBy(adjustment => adjustment.CreatedAt)
            .ToList()
            .AsReadOnly();

    /// <summary>Allocation-free internal read access for domain calculators.</summary>
    internal IReadOnlyList<BalanceAdjustment> GetBalanceAdjustmentEntries() => _balanceAdjustments;

    /// <summary>
    /// Creates a new account deriving <see cref="TrackingStartDate"/> automatically as day 1
    /// of the creation month in Brasília time (<c>America/Sao_Paulo</c>).
    /// </summary>
    public static Account Create(
        Guid id,
        Guid ownerUserId,
        string name,
        Money initialBalance,
        DateTimeOffset createdAtUtc,
        bool isInitialBalanceConfirmed = true)
    {
        DomainValidation.EnsureNotEmpty(id, nameof(id));
        DomainValidation.EnsureNotEmpty(ownerUserId, nameof(ownerUserId), "owner user id");
        var validName = DomainValidation.ValidateRequiredText(name, nameof(name), DomainValidation.MaxNameLength);

        var normalizedCreatedAt = createdAtUtc.ToUniversalTime();
        var trackingStartDate = BrasiliaTime.DeriveTrackingStartDate(normalizedCreatedAt);

        return new Account(
            id,
            ownerUserId,
            validName,
            initialBalance,
            normalizedCreatedAt,
            trackingStartDate,
            isInitialBalanceConfirmed);
    }

    /// <summary>True if a dated entry falls on or after <see cref="TrackingStartDate"/>.</summary>
    public bool CanRegisterOn(DateOnly date) => date >= TrackingStartDate;

    /// <exception cref="DomainException">The date is strictly before <see cref="TrackingStartDate"/>.</exception>
    public void EnsureCanRegisterOn(DateOnly date)
    {
        if (!CanRegisterOn(date))
        {
            throw new DomainException(
                $"No entry can be registered before the tracking start date ({TrackingStartDate:yyyy-MM-dd}).");
        }
    }

    /// <summary>True if the calendar month is on or after <see cref="TrackingStartPeriod"/>.</summary>
    public bool CanRegisterInMonth(YearMonth month)
    {
        ArgumentNullException.ThrowIfNull(month);
        return month >= TrackingStartPeriod;
    }

    /// <exception cref="DomainException">The month is strictly before <see cref="TrackingStartPeriod"/>.</exception>
    public void EnsureCanRegisterInMonth(YearMonth month)
    {
        ArgumentNullException.ThrowIfNull(month);
        if (!CanRegisterInMonth(month))
        {
            throw new DomainException(
                $"No entry can be registered before the tracking start period ({TrackingStartPeriod}).");
        }
    }

    /// <summary>
    /// Validates that a credit card purchase has at least one active installment whose invoice
    /// <see cref="BillingCycle.DueDate"/> falls on or after <see cref="TrackingStartDate"/>.
    /// Purchases whose installments all due before tracking started are rejected.
    /// </summary>
    /// <exception cref="DomainException">No active installment falls within the tracked period.</exception>
    public void EnsureCanRegisterCreditCardPurchase(CreditCardPurchase purchase)
    {
        ArgumentNullException.ThrowIfNull(purchase);

        if (purchase.AccountId != Id)
        {
            throw new DomainException("The credit card purchase does not belong to this account.");
        }

        var hasTrackedInstallment = purchase
            .GetTrackedInstallments(TrackingStartDate)
            .Any();

        if (!hasTrackedInstallment)
        {
            throw new DomainException(
                $"A credit card purchase must have at least one installment due on or after the tracking start date ({TrackingStartDate:yyyy-MM-dd}).");
        }
    }

    /// <summary>
    /// Sets the account's single manual cash balance — always anchored on <see cref="TrackingStartDate"/>
    /// (Day 1 of the entry month) — supporting negative, zero, or positive values, and records an
    /// auditable history entry. Never counts as monthly income, expense, or competence result.
    /// </summary>
    public BalanceAdjustment SetBalance(
        Guid id,
        Money definedBalance,
        string? reason,
        DateTimeOffset createdAtUtc)
    {
        InitialBalance = definedBalance;
        IsInitialBalanceConfirmed = true;

        var entry = BalanceAdjustment.Create(
            id,
            Id,
            TrackingStartDate,
            definedBalance,
            reason,
            createdAtUtc);

        _balanceAdjustments.Add(entry);
        return entry;
    }

    /// <summary>
    /// Removes a balance adjustment record from this account's history.
    /// When a Day-1 opening balance entry is removed, <see cref="InitialBalance"/> reverts to the
    /// latest remaining Day-1 record (or <see cref="Money.Zero"/> if none remain).
    /// Returns the removed entry, or <see langword="null"/> if not found.
    /// </summary>
    public BalanceAdjustment? RemoveBalanceAdjustment(Guid adjustmentId)
    {
        DomainValidation.EnsureNotEmpty(adjustmentId, nameof(adjustmentId), "balance adjustment id");

        var entry = _balanceAdjustments.FirstOrDefault(adjustment => adjustment.Id == adjustmentId);
        if (entry is null)
        {
            return null;
        }

        _balanceAdjustments.Remove(entry);

        if (entry.Date == TrackingStartDate)
        {
            var latestRemaining = _balanceAdjustments
                .Where(adjustment => adjustment.Date == TrackingStartDate)
                .OrderByDescending(adjustment => adjustment.CreatedAt)
                .FirstOrDefault();

            InitialBalance = latestRemaining?.Amount ?? Money.Zero;
        }

        return entry;
    }

    /// <summary>
    /// Completes the one-time initial balance setup on an unconfirmed account.
    /// Once confirmed, <see cref="InitialBalance"/> is immutable.
    /// </summary>
    /// <exception cref="DomainException">The initial balance was already confirmed.</exception>
    public void ConfirmInitialBalance(Money initialBalance)
    {
        if (IsInitialBalanceConfirmed)
        {
            throw new DomainException(
                "The initial balance is immutable once set. Register a balance adjustment instead.");
        }

        InitialBalance = initialBalance;
        IsInitialBalanceConfirmed = true;
    }

    /// <summary>
    /// Skips the one-time initial balance setup by recording zero and locking <see cref="InitialBalance"/>.
    /// </summary>
    public void SkipInitialBalance()
    {
        ConfirmInitialBalance(Money.Zero);
    }

    public void Rename(string name)
    {
        Name = DomainValidation.ValidateRequiredText(name, nameof(name), DomainValidation.MaxNameLength);
    }
}
