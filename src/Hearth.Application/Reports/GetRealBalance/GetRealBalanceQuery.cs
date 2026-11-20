namespace Hearth.Application.Reports.GetRealBalance;

public sealed record GetRealBalanceQuery(Guid AccountId, DateOnly? Until = null);
