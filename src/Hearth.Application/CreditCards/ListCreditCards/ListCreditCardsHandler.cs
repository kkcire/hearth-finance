using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;

namespace Hearth.Application.CreditCards.ListCreditCards;

public sealed class ListCreditCardsHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly ICreditCardRepository _creditCards;
    private readonly ICreditCardPurchaseRepository _purchases;
    private readonly IClock _clock;

    public ListCreditCardsHandler(
        AccountAccessGuard accountAccess,
        ICreditCardRepository creditCards,
        ICreditCardPurchaseRepository purchases,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(creditCards);
        ArgumentNullException.ThrowIfNull(purchases);
        ArgumentNullException.ThrowIfNull(clock);

        _accountAccess = accountAccess;
        _creditCards = creditCards;
        _purchases = purchases;
        _clock = clock;
    }

    public async Task<IReadOnlyList<CreditCardDto>> ExecuteAsync(
        ListCreditCardsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var account = await _accountAccess.GetAuthorizedAccountAsync(query.AccountId, cancellationToken);
        var cards = await _creditCards.GetByAccountAsync(account.Id, cancellationToken);
        var purchases = await _purchases.GetByAccountAsync(account.Id, cancellationToken);
        var today = _clock.Today;

        return cards
            .Where(card => query.IncludeArchived || !card.IsArchived)
            .OrderBy(card => card.IsArchived)
            .ThenBy(card => card.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(card => CreditCardDto.FromEntity(card, today, purchases, account.InitialBalanceDate))
            .ToList();
    }
}
