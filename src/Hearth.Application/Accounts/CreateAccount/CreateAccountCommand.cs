namespace Hearth.Application.Accounts.CreateAccount;

public sealed record CreateAccountCommand(
    string Name,
    decimal InitialBalance,
    bool IsInitialBalanceConfirmed = true);
