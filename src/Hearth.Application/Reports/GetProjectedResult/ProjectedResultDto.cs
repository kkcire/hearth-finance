using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Reports.GetProjectedResult;

public sealed record ProjectedResultDto(
    Guid AccountId,
    int Year,
    int Month,
    decimal Incomes,
    decimal CardInvoice,
    decimal DirectExpenses,
    decimal FixedExpenses,
    decimal Result,
    Guid? ConditionId = null)
{
    internal static ProjectedResultDto FromDomain(Guid accountId, ProjectedResult projected) =>
        new(
            accountId,
            projected.Month.Year,
            projected.Month.Month,
            projected.Incomes.Amount,
            projected.CardInvoice.Amount,
            projected.DirectExpenses.Amount,
            projected.FixedExpenses.Amount,
            projected.Result.Amount,
            projected.ConditionId);
}
