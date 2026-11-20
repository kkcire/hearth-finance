namespace Hearth.Application.Accounts.UpdateInitialBalance;

public sealed record UpdateInitialBalanceCommand(Guid AccountId, decimal InitialBalance);
