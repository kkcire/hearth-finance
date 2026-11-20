using Hearth.Domain.Entities;

namespace Hearth.Application.Abstractions;

public interface IFixedExpenseRepository
{
    Task AddAsync(FixedExpense expense, CancellationToken cancellationToken = default);

    Task<FixedExpense?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FixedExpense>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(FixedExpense expense, CancellationToken cancellationToken = default);

    Task DeleteAsync(FixedExpense expense, CancellationToken cancellationToken = default);
}
