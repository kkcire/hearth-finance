using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Reports.GetMonthlySurplus;

public sealed record MonthlySurplusDto(
    Guid AccountId,
    DateOnly AsOfDate,
    MonthlySurplusPointDto SelectedMonth,
    IReadOnlyList<MonthlySurplusPointDto> Series,
    decimal AverageMonthlyVariation,
    Guid? ConditionId = null)
{
    internal static MonthlySurplusDto FromDomain(Guid accountId, MonthlySurplusSeries series) =>
        new(
            accountId,
            series.AsOfDate,
            MonthlySurplusPointDto.FromDomain(series.SelectedMonth),
            series.Points.Select(MonthlySurplusPointDto.FromDomain).ToList(),
            series.AverageMonthlyVariation.Amount,
            series.ConditionId);
}
