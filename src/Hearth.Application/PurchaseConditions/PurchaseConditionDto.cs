using Hearth.Domain.Entities;

namespace Hearth.Application.PurchaseConditions;

public sealed record PurchaseConditionDto(
    Guid Id,
    string Name,
    bool IsSystem,
    bool IsArchived)
{
    internal static PurchaseConditionDto FromEntity(PurchaseCondition condition) =>
        new(condition.Id, condition.Name, condition.IsSystem, condition.IsArchived);
}
