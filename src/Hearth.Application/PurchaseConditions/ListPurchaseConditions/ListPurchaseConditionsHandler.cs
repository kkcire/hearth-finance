using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Domain;

namespace Hearth.Application.PurchaseConditions.ListPurchaseConditions;

public sealed class ListPurchaseConditionsHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IPurchaseConditionRepository _conditions;

    public ListPurchaseConditionsHandler(
        AccountAccessGuard accountAccess,
        IPurchaseConditionRepository conditions)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(conditions);

        _accountAccess = accountAccess;
        _conditions = conditions;
    }

    public async Task<IReadOnlyList<PurchaseConditionDto>> ExecuteAsync(
        ListPurchaseConditionsQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var userId = _accountAccess.GetRequiredUserId();
        var includeArchived = query?.IncludeArchived ?? false;

        var visible = await _conditions.GetVisibleForUserAsync(userId, cancellationToken);

        return visible
            .Where(condition => includeArchived || !condition.IsArchived)
            .OrderByDescending(condition => condition.Id == SystemPurchaseConditions.OwnId)
            .ThenByDescending(condition => condition.IsSystem)
            .ThenBy(condition => condition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(PurchaseConditionDto.FromEntity)
            .ToList();
    }
}
