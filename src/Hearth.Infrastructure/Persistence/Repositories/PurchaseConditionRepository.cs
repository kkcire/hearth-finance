using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;
using Hearth.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Hearth.Infrastructure.Persistence.Repositories;

internal sealed class PurchaseConditionRepository : IPurchaseConditionRepository
{
    private readonly HearthDbContext _context;

    public PurchaseConditionRepository(HearthDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task AddAsync(PurchaseCondition condition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(condition);
        await _context.PurchaseConditions.AddAsync(condition, cancellationToken);
    }

    public Task<PurchaseCondition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.PurchaseConditions.FirstOrDefaultAsync(condition => condition.Id == id, cancellationToken);

    public async Task<IReadOnlyList<PurchaseCondition>> GetVisibleForUserAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default) =>
        await _context.PurchaseConditions
            .Where(condition => condition.OwnerUserId == null || condition.OwnerUserId == ownerUserId)
            .ToListAsync(cancellationToken);

    public Task<PurchaseCondition?> FindByNameForUserAsync(
        Guid ownerUserId,
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalized = name.Trim().ToLowerInvariant();

        return _context.PurchaseConditions
            .Where(condition =>
                (condition.OwnerUserId == null || condition.OwnerUserId == ownerUserId)
                && EF.Property<string>(condition, PurchaseConditionConfiguration.NormalizedNameShadowProperty) == normalized)
            .OrderBy(condition => condition.OwnerUserId != null)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task UpdateAsync(PurchaseCondition condition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(condition);

        if (_context.Entry(condition).State == EntityState.Detached)
        {
            _context.PurchaseConditions.Update(condition);
        }

        return Task.CompletedTask;
    }
}
