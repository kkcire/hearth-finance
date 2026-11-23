using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Reports.GetMonthlySurplus;

public sealed record MonthlySurplusDto(
    Guid AccountId,
    DateOnly AsOfDate,
    MonthlySurplusPointDto SelectedMonth,
    IReadOnlyList<MonthlySurplusPointDto> Series,
    decimal AverageMonthlyVariation)
{
    internal static MonthlySurplusDto FromDomain(Guid accountId, MonthlySurplusSeries series) =>
        new(
            accountId,
            series.AsOfDate,
            MonthlySurplusPointDto.FromDomain(series.SelectedMonth),
            series.Points.Select(MonthlySurplusPointDto.FromDomain).ToList(),
            series.AverageMonthlyVariation.Amount);
}
