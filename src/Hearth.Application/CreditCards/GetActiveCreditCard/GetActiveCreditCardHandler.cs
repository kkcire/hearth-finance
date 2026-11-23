using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;

namespace Hearth.Application.CreditCards.GetActiveCreditCard;

public sealed class GetActiveCreditCardHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly ICreditCardRepository _creditCards;
    private readonly ICreditCardPurchaseRepository _purchases;
    private readonly IClock _clock;

    public GetActiveCreditCardHandler(
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

    public async Task<CreditCardDto?> ExecuteAsync(
        GetActiveCreditCardQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var account = await _accountAccess.GetAuthorizedAccountAsync(query.AccountId, cancellationToken);
        var card = await _creditCards.GetActiveByAccountAsync(account.Id, cancellationToken);
        if (card is null)
        {
            return null;
        }

        var purchases = await _purchases.GetByCreditCardAsync(card.Id, cancellationToken);
        return CreditCardDto.FromEntity(card, _clock.Today, purchases);
    }
}
