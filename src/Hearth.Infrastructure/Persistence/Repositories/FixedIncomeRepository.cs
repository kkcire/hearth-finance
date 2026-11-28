using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hearth.Infrastructure.Persistence.Repositories;

internal sealed class FixedIncomeRepository : IFixedIncomeRepository
{
    private readonly HearthDbContext _context;

    public FixedIncomeRepository(HearthDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task AddAsync(FixedIncome income, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(income);
        await _context.FixedIncomes.AddAsync(income, cancellationToken);
    }

    public Task<FixedIncome?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        QueryWithCollections()
            .FirstOrDefaultAsync(income => income.Id == id, cancellationToken);

    public async Task<IReadOnlyList<FixedIncome>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default) =>
        await QueryWithCollections()
            .Where(income => income.AccountId == accountId)
            .ToListAsync(cancellationToken);

    public Task UpdateAsync(FixedIncome income, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(income);

        if (_context.Entry(income).State == EntityState.Detached)
        {
            _context.FixedIncomes.Update(income);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(FixedIncome income, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(income);
        _context.FixedIncomes.Remove(income);
        return Task.CompletedTask;
    }

    private IQueryable<FixedIncome> QueryWithCollections() =>
        _context.FixedIncomes
            .Include("_amountHistory")
            .Include("_receipts")
            .AsSplitQuery();
}
