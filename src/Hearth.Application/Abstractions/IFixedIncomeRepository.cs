using Hearth.Domain.Entities;

namespace Hearth.Application.Abstractions;

public interface IFixedIncomeRepository
{
    Task AddAsync(FixedIncome income, CancellationToken cancellationToken = default);

    Task<FixedIncome?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FixedIncome>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(FixedIncome income, CancellationToken cancellationToken = default);

    Task DeleteAsync(FixedIncome income, CancellationToken cancellationToken = default);
}
