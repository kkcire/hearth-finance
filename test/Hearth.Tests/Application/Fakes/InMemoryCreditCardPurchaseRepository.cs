using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;

namespace Hearth.Tests.Application.Fakes;

internal sealed class InMemoryCreditCardPurchaseRepository : ICreditCardPurchaseRepository
{
    private readonly Dictionary<Guid, CreditCardPurchase> _purchases = new();

    public IReadOnlyCollection<CreditCardPurchase> Purchases => _purchases.Values;

    public int UpdateCount { get; private set; }

    public int DeleteCount { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task AddAsync(CreditCardPurchase purchase, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _purchases.Add(purchase.Id, purchase);
        return Task.CompletedTask;
    }

    public Task<CreditCardPurchase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        return Task.FromResult(_purchases.GetValueOrDefault(id));
    }

    public Task<IReadOnlyList<CreditCardPurchase>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        IReadOnlyList<CreditCardPurchase> result = _purchases.Values
            .Where(purchase => purchase.AccountId == accountId)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<bool> AnyByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        return Task.FromResult(_purchases.Values.Any(purchase => purchase.CategoryId == categoryId));
    }

    public Task UpdateAsync(CreditCardPurchase purchase, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _purchases[purchase.Id] = purchase;
        UpdateCount++;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(CreditCardPurchase purchase, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _purchases.Remove(purchase.Id);
        DeleteCount++;
        return Task.CompletedTask;
    }
}
