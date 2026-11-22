using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hearth.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository : IUserRepository
{
    private readonly HearthDbContext _context;

    public UserRepository(HearthDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        await _context.Users.AddAsync(user, cancellationToken);
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Users.FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        _context.Users.FirstOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);

    public Task<bool> ExistsWithEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        _context.Users.AnyAsync(user => user.Email == normalizedEmail, cancellationToken);

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (_context.Entry(user).State == EntityState.Detached)
        {
            _context.Users.Update(user);
        }

        return Task.CompletedTask;
    }
}
