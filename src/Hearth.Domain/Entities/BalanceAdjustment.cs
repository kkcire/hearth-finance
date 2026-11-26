using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Entities;

/// <summary>
/// An immutable cash-tracking audit record capturing a manual update to the account's single
/// Day-1 opening balance (<see cref="Account.InitialBalance"/> on <see cref="Account.TrackingStartDate"/>).
/// Supports negative, zero, or positive values and impacts real cash balance (<c>SALDO</c>) only —
/// strictly excluded from monthly competence metrics (income, expenses, monthly result, category spending, and savings rate).
/// </summary>
public sealed class BalanceAdjustment
{
    private BalanceAdjustment()
    {
    }

    private BalanceAdjustment(
        Guid id,
        Guid accountId,
        DateOnly date,
        Money amount,
        string? reason,
        DateTimeOffset createdAt)
    {
        Id = id;
        AccountId = accountId;
        Date = date;
        Amount = amount;
        Reason = reason;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid AccountId { get; }

    /// <summary>Effective cash date of the defined balance (always within [<see cref="Account.TrackingStartDate"/>, today]).</summary>
    public DateOnly Date { get; }

    /// <summary>Directly defined cash balance on <see cref="Date"/> (negative, zero, or positive).</summary>
    public Money Amount { get; }

    /// <summary>Optional human-readable note explaining the cash reconciliation.</summary>
    public string? Reason { get; }

    /// <summary>UTC timestamp when the balance entry was recorded.</summary>
    public DateTimeOffset CreatedAt { get; }

    internal static BalanceAdjustment Create(
        Guid id,
        Guid accountId,
        DateOnly date,
        Money definedBalance,
        string? reason,
        DateTimeOffset createdAtUtc)
    {
        DomainValidation.EnsureNotEmpty(id, nameof(id));
        DomainValidation.EnsureNotEmpty(accountId, nameof(accountId), "account id");

        var normalizedReason = NormalizeReason(reason);

        return new BalanceAdjustment(
            id,
            accountId,
            date,
            definedBalance,
            normalizedReason,
            createdAtUtc.ToUniversalTime());
    }

    private static string? NormalizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        var trimmed = reason.Trim();
        if (trimmed.Length > DomainValidation.MaxDescriptionLength)
        {
            throw new DomainException(
                $"The balance definition reason cannot exceed {DomainValidation.MaxDescriptionLength} characters.");
        }

        return trimmed;
    }
}
