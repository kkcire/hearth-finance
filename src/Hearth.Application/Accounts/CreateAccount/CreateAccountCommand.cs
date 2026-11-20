namespace Hearth.Application.Accounts.CreateAccount;

public sealed record CreateAccountCommand(
    string Name,
    decimal InitialBalance,
    DateOnly InitialBalanceDate,
    int ClosingDay,
    int DueDay);
