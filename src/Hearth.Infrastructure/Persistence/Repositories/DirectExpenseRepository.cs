using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hearth.Infrastructure.Persistence.Repositories;

internal sealed class DirectExpenseRepository : IDirectExpenseRepository
{
    private readonly HearthDbContext _context;

    public DirectExpenseRepository(HearthDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task AddAsync(DirectExpense expense, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expense);
        await _context.DirectExpenses.AddAsync(expense, cancellationToken);
    }

    public Task<DirectExpense?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.DirectExpenses.FirstOrDefaultAsync(expense => expense.Id == id, cancellationToken);

    public async Task<IReadOnlyList<DirectExpense>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default) =>
        await _context.DirectExpenses
            .Where(expense => expense.AccountId == accountId)
            .ToListAsync(cancellationToken);

    public Task<bool> AnyByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        _context.DirectExpenses.AnyAsync(expense => expense.CategoryId == categoryId, cancellationToken);

    public Task UpdateAsync(DirectExpense expense, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expense);

        if (_context.Entry(expense).State == EntityState.Detached)
        {
            _context.DirectExpenses.Update(expense);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(DirectExpense expense, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expense);
        _context.DirectExpenses.Remove(expense);
        return Task.CompletedTask;
    }
}
