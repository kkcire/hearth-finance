using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;

namespace Hearth.Tests.Application.Fakes;

internal sealed class InMemoryUserRepository : IUserRepository
{
    private readonly Dictionary<Guid, User> _users = new();

    public IReadOnlyCollection<User> Users => _users.Values;

    public int UpdateCount { get; private set; }

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        _users.Add(user.Id, user);
        return Task.CompletedTask;
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.GetValueOrDefault(id));

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.Values.FirstOrDefault(u => u.Email == normalizedEmail));

    public Task<bool> ExistsWithEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.Values.Any(u => u.Email == normalizedEmail));

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _users[user.Id] = user;
        UpdateCount++;
        return Task.CompletedTask;
    }
}
