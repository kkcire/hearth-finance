namespace Hearth.Application.Accounts.ConfirmInitialBalance;

public sealed record ConfirmInitialBalanceCommand(
    Guid AccountId,
    decimal InitialBalance,
    DateOnly InitialBalanceDate,
    bool Skip = false);
