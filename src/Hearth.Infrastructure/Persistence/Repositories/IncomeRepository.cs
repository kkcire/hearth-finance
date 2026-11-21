using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hearth.Infrastructure.Persistence.Repositories;

internal sealed class IncomeRepository : IIncomeRepository
{
    private readonly HearthDbContext _context;

    public IncomeRepository(HearthDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task AddAsync(Income income, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(income);
        await _context.Incomes.AddAsync(income, cancellationToken);
    }

    public Task<Income?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Incomes.FirstOrDefaultAsync(income => income.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Income>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default) =>
        await _context.Incomes
            .Where(income => income.AccountId == accountId)
            .ToListAsync(cancellationToken);

    public Task UpdateAsync(Income income, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(income);

        if (_context.Entry(income).State == EntityState.Detached)
        {
            _context.Incomes.Update(income);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Income income, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(income);
        _context.Incomes.Remove(income);
        return Task.CompletedTask;
    }
}
