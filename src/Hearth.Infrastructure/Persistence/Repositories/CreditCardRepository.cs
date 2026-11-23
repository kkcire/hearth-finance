using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hearth.Infrastructure.Persistence.Repositories;

internal sealed class CreditCardRepository : ICreditCardRepository
{
    private readonly HearthDbContext _context;

    public CreditCardRepository(HearthDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task AddAsync(CreditCard card, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        await _context.CreditCards.AddAsync(card, cancellationToken);
    }

    public Task<CreditCard?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.CreditCards.FirstOrDefaultAsync(card => card.Id == id, cancellationToken);

    public Task<CreditCard?> GetActiveByAccountAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        _context.CreditCards.FirstOrDefaultAsync(
            card => card.AccountId == accountId && !card.IsArchived,
            cancellationToken);

    public async Task<IReadOnlyList<CreditCard>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default) =>
        await _context.CreditCards
            .Where(card => card.AccountId == accountId)
            .ToListAsync(cancellationToken);

    public Task UpdateAsync(CreditCard card, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (_context.Entry(card).State == EntityState.Detached)
        {
            _context.CreditCards.Update(card);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(CreditCard card, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        _context.CreditCards.Remove(card);
        return Task.CompletedTask;
    }
}
