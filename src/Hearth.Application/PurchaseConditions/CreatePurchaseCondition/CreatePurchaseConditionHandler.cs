using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Domain;
using Hearth.Domain.Entities;

namespace Hearth.Application.PurchaseConditions.CreatePurchaseCondition;

public sealed class CreatePurchaseConditionHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IPurchaseConditionRepository _conditions;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePurchaseConditionHandler(
        AccountAccessGuard accountAccess,
        IPurchaseConditionRepository conditions,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _conditions = conditions;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Creates a custom purchase condition for the authenticated user, or idempotently reuses
    /// an existing visible condition with the same name (case-insensitive after trimming).
    /// If a matching user condition exists and is archived, it is restored and returned.
    /// </summary>
    public async Task<PurchaseConditionDto> ExecuteAsync(
        CreatePurchaseConditionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var userId = _accountAccess.GetRequiredUserId();
        var candidate = PurchaseCondition.CreateForUser(Guid.CreateVersion7(), userId, command.Name);

        var existing = await _conditions.FindByNameForUserAsync(userId, candidate.Name, cancellationToken);
        if (existing is not null)
        {
            if (existing.IsArchived)
            {
                existing.Restore();
                await _conditions.UpdateAsync(existing, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return PurchaseConditionDto.FromEntity(existing);
        }

        await _conditions.AddAsync(candidate, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return PurchaseConditionDto.FromEntity(candidate);
    }
}
