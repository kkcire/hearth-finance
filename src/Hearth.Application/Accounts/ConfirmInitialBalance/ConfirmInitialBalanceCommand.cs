namespace Hearth.Application.Accounts.ConfirmInitialBalance;

public sealed record ConfirmInitialBalanceCommand(
    Guid AccountId,
    decimal InitialBalance,
    bool Skip = false);
