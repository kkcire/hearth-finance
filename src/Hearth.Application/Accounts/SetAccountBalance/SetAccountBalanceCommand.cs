namespace Hearth.Application.Accounts.SetAccountBalance;

public sealed record SetAccountBalanceCommand(
    Guid AccountId,
    decimal Balance,
    string? Reason = null);
