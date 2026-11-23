using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hearth.Infrastructure.Persistence.Repositories;

internal sealed class CreditCardPurchaseRepository : ICreditCardPurchaseRepository
{
    private readonly HearthDbContext _context;

    public CreditCardPurchaseRepository(HearthDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task AddAsync(CreditCardPurchase purchase, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(purchase);
        await _context.CreditCardPurchases.AddAsync(purchase, cancellationToken);
    }

    public Task<CreditCardPurchase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.CreditCardPurchases.FirstOrDefaultAsync(purchase => purchase.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CreditCardPurchase>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default) =>
        await _context.CreditCardPurchases
            .Where(purchase => purchase.AccountId == accountId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CreditCardPurchase>> GetByCreditCardAsync(
        Guid creditCardId,
        CancellationToken cancellationToken = default) =>
        await _context.CreditCardPurchases
            .Where(purchase => purchase.CreditCardId == creditCardId)
            .ToListAsync(cancellationToken);

    public Task<bool> AnyByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        _context.CreditCardPurchases.AnyAsync(purchase => purchase.CategoryId == categoryId, cancellationToken);

    public Task<bool> AnyByCreditCardAsync(Guid creditCardId, CancellationToken cancellationToken = default) =>
        _context.CreditCardPurchases.AnyAsync(purchase => purchase.CreditCardId == creditCardId, cancellationToken);

    public Task UpdateAsync(CreditCardPurchase purchase, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(purchase);

        if (_context.Entry(purchase).State == EntityState.Detached)
        {
            _context.CreditCardPurchases.Update(purchase);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(CreditCardPurchase purchase, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(purchase);
        _context.CreditCardPurchases.Remove(purchase);
        return Task.CompletedTask;
    }
}
