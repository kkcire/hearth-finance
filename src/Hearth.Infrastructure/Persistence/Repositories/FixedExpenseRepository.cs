using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hearth.Infrastructure.Persistence.Repositories;

internal sealed class FixedExpenseRepository : IFixedExpenseRepository
{
    private readonly HearthDbContext _context;

    public FixedExpenseRepository(HearthDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task AddAsync(FixedExpense expense, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expense);
        await _context.FixedExpenses.AddAsync(expense, cancellationToken);
    }

    public Task<FixedExpense?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.FixedExpenses.FirstOrDefaultAsync(expense => expense.Id == id, cancellationToken);

    public async Task<IReadOnlyList<FixedExpense>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default) =>
        await _context.FixedExpenses
            .Where(expense => expense.AccountId == accountId)
            .ToListAsync(cancellationToken);

    public Task<bool> AnyByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        _context.FixedExpenses.AnyAsync(expense => expense.CategoryId == categoryId, cancellationToken);

    public Task UpdateAsync(FixedExpense expense, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expense);

        if (_context.Entry(expense).State == EntityState.Detached)
        {
            _context.FixedExpenses.Update(expense);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(FixedExpense expense, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expense);
        _context.FixedExpenses.Remove(expense);
        return Task.CompletedTask;
    }
}
