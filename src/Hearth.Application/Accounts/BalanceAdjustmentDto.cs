using Hearth.Domain.Entities;

namespace Hearth.Application.Accounts;

public sealed record BalanceAdjustmentDto(
    Guid Id,
    DateOnly Date,
    decimal Amount,
    string? Reason,
    DateTimeOffset CreatedAt)
{
    public static BalanceAdjustmentDto FromEntity(BalanceAdjustment adjustment)
    {
        ArgumentNullException.ThrowIfNull(adjustment);

        return new BalanceAdjustmentDto(
            adjustment.Id,
            adjustment.Date,
            adjustment.Amount.Amount,
            adjustment.Reason,
            adjustment.CreatedAt);
    }
}
