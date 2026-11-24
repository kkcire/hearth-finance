namespace Hearth.Application.Reports.GetProjectedResult;

public sealed record GetProjectedResultQuery(Guid AccountId, int Year, int Month, Guid? ConditionId = null);
