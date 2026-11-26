using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;

namespace Hearth.Tests.Application.Fakes;

internal sealed class InMemoryAccountRepository : IAccountRepository
{
    private readonly Dictionary<Guid, Account> _accounts = new();

    public IReadOnlyCollection<Account> Accounts => _accounts.Values;

    public int UpdateCount { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task AddAsync(Account account, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _accounts.Add(account.Id, account);
        return Task.CompletedTask;
    }

    public Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        return Task.FromResult(_accounts.GetValueOrDefault(id));
    }

    public Task<Account?> GetByIdWithBalanceHistoryAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Account>> GetByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        IReadOnlyList<Account> result = _accounts.Values
            .Where(account => account.OwnerUserId == ownerUserId)
            .ToList();
        return Task.FromResult(result);
    }

    public Task UpdateAsync(Account account, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _accounts[account.Id] = account;
        UpdateCount++;
        return Task.CompletedTask;
    }

    public Task AddBalanceAdjustmentAsync(BalanceAdjustment adjustment, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        return Task.CompletedTask;
    }

    public Task RemoveBalanceAdjustmentAsync(BalanceAdjustment adjustment, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        return Task.CompletedTask;
    }
}
