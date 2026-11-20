using Hearth.Domain.Entities;

namespace Hearth.Application.Abstractions;

public interface IIncomeRepository
{
    Task AddAsync(Income income, CancellationToken cancellationToken = default);

    Task<Income?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Income>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(Income income, CancellationToken cancellationToken = default);

    Task DeleteAsync(Income income, CancellationToken cancellationToken = default);
}
