using Hearth.Domain.Entities;

namespace Hearth.Application.Abstractions;

public interface ICreditCardRepository
{
    Task AddAsync(CreditCard card, CancellationToken cancellationToken = default);

    Task<CreditCard?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CreditCard?> GetActiveByAccountAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CreditCard>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(CreditCard card, CancellationToken cancellationToken = default);

    Task DeleteAsync(CreditCard card, CancellationToken cancellationToken = default);
}
