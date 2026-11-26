using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Accounts;

public sealed record AccountDto(
    Guid Id,
    string Name,
    decimal InitialBalance,
    DateOnly TrackingStartDate,
    bool IsInitialBalanceConfirmed,
    DateTimeOffset CreatedAt = default,
    IReadOnlyList<BalanceAdjustmentDto>? BalanceAdjustments = null)
{
    public YearMonth TrackingStartPeriod => YearMonth.From(TrackingStartDate);

    /// <summary>Alias for <see cref="TrackingStartDate"/>.</summary>
    public DateOnly InitialBalanceDate => TrackingStartDate;

    public static AccountDto FromEntity(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);

        var adjustments = account.BalanceAdjustments
            .OrderByDescending(entry => entry.CreatedAt)
            .Select(BalanceAdjustmentDto.FromEntity)
            .ToList()
            .AsReadOnly();

        return new AccountDto(
            account.Id,
            account.Name,
            account.InitialBalance.Amount,
            account.TrackingStartDate,
            account.IsInitialBalanceConfirmed,
            account.CreatedAt,
            adjustments);
    }
}
