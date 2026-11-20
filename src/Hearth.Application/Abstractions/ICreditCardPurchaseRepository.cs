using Hearth.Domain.Entities;

namespace Hearth.Application.Abstractions;

public interface ICreditCardPurchaseRepository
{
    Task AddAsync(CreditCardPurchase purchase, CancellationToken cancellationToken = default);

    Task<CreditCardPurchase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CreditCardPurchase>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<bool> AnyByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);

    Task UpdateAsync(CreditCardPurchase purchase, CancellationToken cancellationToken = default);

    Task DeleteAsync(CreditCardPurchase purchase, CancellationToken cancellationToken = default);
}
