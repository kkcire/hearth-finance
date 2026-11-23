using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;

namespace Hearth.Tests.Application.Fakes;

internal sealed class InMemoryCreditCardRepository : ICreditCardRepository
{
    private readonly Dictionary<Guid, CreditCard> _cards = new();

    public IReadOnlyCollection<CreditCard> Cards => _cards.Values;

    public int UpdateCount { get; private set; }

    public int DeleteCount { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task AddAsync(CreditCard card, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _cards.Add(card.Id, card);
        return Task.CompletedTask;
    }

    public Task<CreditCard?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        return Task.FromResult(_cards.GetValueOrDefault(id));
    }

    public Task<CreditCard?> GetActiveByAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        return Task.FromResult(_cards.Values.FirstOrDefault(c => c.AccountId == accountId && !c.IsArchived));
    }

    public Task<IReadOnlyList<CreditCard>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        IReadOnlyList<CreditCard> result = _cards.Values
            .Where(c => c.AccountId == accountId)
            .ToList();
        return Task.FromResult(result);
    }

    public Task UpdateAsync(CreditCard card, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _cards[card.Id] = card;
        UpdateCount++;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(CreditCard card, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _cards.Remove(card.Id);
        DeleteCount++;
        return Task.CompletedTask;
    }
}
