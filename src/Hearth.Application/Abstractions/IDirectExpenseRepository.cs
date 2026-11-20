using Hearth.Domain.Entities;

namespace Hearth.Application.Abstractions;

public interface IDirectExpenseRepository
{
    Task AddAsync(DirectExpense expense, CancellationToken cancellationToken = default);

    Task<DirectExpense?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DirectExpense>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(DirectExpense expense, CancellationToken cancellationToken = default);

    Task DeleteAsync(DirectExpense expense, CancellationToken cancellationToken = default);
}
