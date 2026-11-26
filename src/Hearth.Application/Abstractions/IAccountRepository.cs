using Hearth.Domain.Entities;

namespace Hearth.Application.Abstractions;

public interface IAccountRepository
{
    Task AddAsync(Account account, CancellationToken cancellationToken = default);

    Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Account?> GetByIdWithBalanceHistoryAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Account>> GetByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default);

    Task UpdateAsync(Account account, CancellationToken cancellationToken = default);

    Task AddBalanceAdjustmentAsync(BalanceAdjustment adjustment, CancellationToken cancellationToken = default);

    Task RemoveBalanceAdjustmentAsync(BalanceAdjustment adjustment, CancellationToken cancellationToken = default);
}
