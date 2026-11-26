namespace Hearth.Application.Accounts.DeleteBalanceAdjustment;

public sealed record DeleteBalanceAdjustmentCommand(
    Guid AccountId,
    Guid AdjustmentId);
