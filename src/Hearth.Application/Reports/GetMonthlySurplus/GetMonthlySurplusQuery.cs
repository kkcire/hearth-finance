using Hearth.Domain.Services;

namespace Hearth.Application.Reports.GetMonthlySurplus;

public sealed record GetMonthlySurplusQuery(
    Guid AccountId,
    int Year,
    int Month,
    int PastMonths = MonthlySurplusCalculator.DefaultPastMonths,
    int FutureMonths = MonthlySurplusCalculator.DefaultFutureMonths);
