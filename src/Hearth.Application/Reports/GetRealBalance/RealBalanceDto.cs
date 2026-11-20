using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Reports.GetRealBalance;

public sealed record RealBalanceDto(
    Guid AccountId,
    DateOnly Until,
    decimal InitialBalance,
    decimal Incomes,
    decimal DirectExpenses,
    decimal FixedExpenses,
    decimal CardInvoices,
    decimal Balance)
{
    internal static RealBalanceDto FromDomain(Guid accountId, RealBalance realBalance) =>
        new(
            accountId,
            realBalance.Until,
            realBalance.InitialBalance.Amount,
            realBalance.Incomes.Amount,
            realBalance.DirectExpenses.Amount,
            realBalance.FixedExpenses.Amount,
            realBalance.CardInvoices.Amount,
            realBalance.Balance.Amount);
}
