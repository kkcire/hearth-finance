using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.PurchaseConditions;

internal static class PurchaseConditionValidation
{
    public static void EnsureOptionalFilterNotEmpty(Guid? conditionId, string paramName)
    {
        if (conditionId == Guid.Empty)
        {
            throw new ArgumentException("The condition id must not be empty.", paramName);
        }
    }

    public static async Task EnsureUsableForUserAsync(
        IPurchaseConditionRepository conditions,
        Guid ownerUserId,
        Guid conditionId,
        CancellationToken cancellationToken)
    {
        var condition = await conditions.GetByIdAsync(conditionId, cancellationToken)
                        ?? throw new NotFoundException(nameof(PurchaseCondition), conditionId);

        if (!condition.IsSystem && condition.OwnerUserId != ownerUserId)
        {
            throw new NotFoundException(nameof(PurchaseCondition), conditionId);
        }

        condition.EnsureUsable();
    }
}
