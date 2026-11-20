using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hearth.Infrastructure.Persistence.Repositories;

internal sealed class CategoryBudgetRepository : ICategoryBudgetRepository
{
    private readonly HearthDbContext _context;

    public CategoryBudgetRepository(HearthDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task AddAsync(CategoryBudget budget, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(budget);
        await _context.CategoryBudgets.AddAsync(budget, cancellationToken);
    }

    public Task<CategoryBudget?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.CategoryBudgets.FirstOrDefaultAsync(budget => budget.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CategoryBudget>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default) =>
        await _context.CategoryBudgets
            .Where(budget => budget.AccountId == accountId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CategoryBudget>> GetByAccountAndCategoryAsync(
        Guid accountId,
        Guid categoryId,
        CancellationToken cancellationToken = default) =>
        await _context.CategoryBudgets
            .Where(budget => budget.AccountId == accountId && budget.CategoryId == categoryId)
            .ToListAsync(cancellationToken);

    public Task<bool> AnyByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        _context.CategoryBudgets.AnyAsync(budget => budget.CategoryId == categoryId, cancellationToken);

    public Task UpdateAsync(CategoryBudget budget, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(budget);

        if (_context.Entry(budget).State == EntityState.Detached)
        {
            _context.CategoryBudgets.Update(budget);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(CategoryBudget budget, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(budget);
        _context.CategoryBudgets.Remove(budget);
        return Task.CompletedTask;
    }
}
