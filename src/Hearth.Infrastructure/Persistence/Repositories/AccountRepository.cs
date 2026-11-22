using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hearth.Infrastructure.Persistence.Repositories;

internal sealed class AccountRepository : IAccountRepository
{
    private readonly HearthDbContext _context;

    public AccountRepository(HearthDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task AddAsync(Account account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        await _context.Accounts.AddAsync(account, cancellationToken);
    }

    public Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Accounts.FirstOrDefaultAsync(account => account.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Account>> GetByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default) =>
        await _context.Accounts
            .Where(account => account.OwnerUserId == ownerUserId)
            .ToListAsync(cancellationToken);

    public Task UpdateAsync(Account account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (_context.Entry(account).State == EntityState.Detached)
        {
            _context.Accounts.Update(account);
        }

        return Task.CompletedTask;
    }
}
