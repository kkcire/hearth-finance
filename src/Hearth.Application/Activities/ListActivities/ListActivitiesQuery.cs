namespace Hearth.Application.Activities.ListActivities;

public sealed record ListActivitiesQuery(
    Guid AccountId,
    int? Year = null,
    int? Month = null,
    Guid? ConditionId = null,
    string? MovementType = null);
