using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;
using Hearth.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Hearth.Infrastructure.Persistence.Repositories;

internal sealed class CategoryRepository : ICategoryRepository
{
    private readonly HearthDbContext _context;

    public CategoryRepository(HearthDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);
        await _context.Categories.AddAsync(category, cancellationToken);
    }

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Categories.FirstOrDefaultAsync(category => category.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Category>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default) =>
        await _context.Categories
            .Where(category => category.AccountId == accountId)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsWithNameAsync(
        Guid accountId,
        string name,
        Guid? excludeCategoryId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalized = name.Trim().ToLowerInvariant();

        var query = _context.Categories.Where(category =>
            category.AccountId == accountId
            && EF.Property<string>(category, CategoryConfiguration.NormalizedNameShadowProperty) == normalized);

        if (excludeCategoryId.HasValue)
        {
            var excludedId = excludeCategoryId.Value;
            query = query.Where(category => category.Id != excludedId);
        }

        return query.AnyAsync(cancellationToken);
    }

    public Task UpdateAsync(Category category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);

        if (_context.Entry(category).State == EntityState.Detached)
        {
            _context.Categories.Update(category);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Category category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);
        _context.Categories.Remove(category);
        return Task.CompletedTask;
    }
}
