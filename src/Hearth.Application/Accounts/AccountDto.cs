using Hearth.Domain.Entities;

namespace Hearth.Application.Accounts;

public sealed record AccountDto(
    Guid Id,
    string Name,
    decimal InitialBalance,
    DateOnly InitialBalanceDate,
    int ClosingDay,
    int DueDay)
{
    internal static AccountDto FromEntity(Account account) =>
        new(
            account.Id,
            account.Name,
            account.InitialBalance.Amount,
            account.InitialBalanceDate,
            account.BillingRule.ClosingDay,
            account.BillingRule.DueDay);
}
