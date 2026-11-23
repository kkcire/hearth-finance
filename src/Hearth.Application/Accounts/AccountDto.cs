using Hearth.Domain.Entities;

namespace Hearth.Application.Accounts;

public sealed record AccountDto(
    Guid Id,
    string Name,
    decimal InitialBalance,
    DateOnly InitialBalanceDate,
    bool IsInitialBalanceConfirmed)
{
    public static AccountDto FromEntity(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return new AccountDto(
            account.Id,
            account.Name,
            account.InitialBalance.Amount,
            account.InitialBalanceDate,
            account.IsInitialBalanceConfirmed);
    }
}
