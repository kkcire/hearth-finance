namespace Hearth.Application.Activities.ListActivities;

public sealed record ActivityItemDto(
    Guid Id,
    string Description,
    DateOnly Date,
    decimal Amount,
    string MovementType,
    string CategoryName,
    string CategoryIcon,
    string StatusLabel,
    string? InstallmentInfo);
