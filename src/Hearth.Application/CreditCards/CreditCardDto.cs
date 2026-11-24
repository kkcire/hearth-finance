using Hearth.Domain.Entities;
using Hearth.Domain.Services;

namespace Hearth.Application.CreditCards;

public sealed record CreditCardDto(
    Guid Id,
    Guid AccountId,
    string? Name,
    int ClosingDay,
    int DueDay,
    decimal? Limit,
    decimal UsedLimit,
    decimal? AvailableLimit,
    bool IsArchived)
{
    public static CreditCardDto FromEntity(
        CreditCard card,
        DateOnly today,
        IEnumerable<CreditCardPurchase> purchases,
        DateOnly? initialBalanceDate = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(purchases);

        var used = CreditCardLimitCalculator.CalculateUsedLimit(card.Id, today, purchases, initialBalanceDate);
        var available = CreditCardLimitCalculator.CalculateAvailableLimit(card, today, purchases, initialBalanceDate);

        return new CreditCardDto(
            card.Id,
            card.AccountId,
            card.Name,
            card.BillingRule.ClosingDay,
            card.BillingRule.DueDay,
            card.Limit?.Amount,
            used.Amount,
            available?.Amount,
            card.IsArchived);
    }
}
