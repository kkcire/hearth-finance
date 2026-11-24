using Hearth.Application.Abstractions;
using Hearth.Domain;
using Hearth.Domain.Entities;

namespace Hearth.Tests.Application.Fakes;

internal sealed class InMemoryPurchaseConditionRepository : IPurchaseConditionRepository
{
    private readonly Dictionary<Guid, PurchaseCondition> _conditions = new();

    public InMemoryPurchaseConditionRepository(bool seedSystemConditions = true)
    {
        if (seedSystemConditions)
        {
            foreach (var systemCondition in SystemPurchaseConditions.CreateAll())
            {
                _conditions.Add(systemCondition.Id, systemCondition);
            }
        }
    }

    public IReadOnlyCollection<PurchaseCondition> Conditions => _conditions.Values;

    public int UpdateCount { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task AddAsync(PurchaseCondition condition, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _conditions.Add(condition.Id, condition);
        return Task.CompletedTask;
    }

    public Task<PurchaseCondition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        return Task.FromResult(_conditions.GetValueOrDefault(id));
    }

    public Task<IReadOnlyList<PurchaseCondition>> GetVisibleForUserAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        IReadOnlyList<PurchaseCondition> result = _conditions.Values
            .Where(condition => condition.IsSystem || condition.OwnerUserId == ownerUserId)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<PurchaseCondition?> FindByNameForUserAsync(
        Guid ownerUserId,
        string name,
        CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        var match = _conditions.Values
            .Where(condition =>
                (condition.IsSystem || condition.OwnerUserId == ownerUserId)
                && string.Equals(condition.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(condition => condition.IsSystem)
            .FirstOrDefault();
        return Task.FromResult(match);
    }

    public Task UpdateAsync(PurchaseCondition condition, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _conditions[condition.Id] = condition;
        UpdateCount++;
        return Task.CompletedTask;
    }
}
