using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Services;

/// <summary>
/// Pure calculations for credit card limit consumption.
/// Maintains exact symmetry with <see cref="RealBalanceCalculator"/>:
/// installments with <c>DueDate &lt;= today</c> have settled in cash balance, while
/// installments with <c>DueDate &gt; today</c> occupy credit card limit.
/// </summary>
public static class CreditCardLimitCalculator
{
    public static Money CalculateUsedLimit(
        Guid creditCardId,
        DateOnly today,
        IEnumerable<CreditCardPurchase> purchases)
    {
        DomainValidation.EnsureNotEmpty(creditCardId, nameof(creditCardId), "credit card id");
        ArgumentNullException.ThrowIfNull(purchases);

        return Money.Sum(
            purchases
                .Where(purchase => purchase.CreditCardId == creditCardId)
                .SelectMany(purchase => purchase.GetActiveInstallments())
                .Where(installment => installment.Cycle.DueDate > today)
                .Select(installment => installment.Amount));
    }

    public static Money? CalculateAvailableLimit(
        CreditCard card,
        DateOnly today,
        IEnumerable<CreditCardPurchase> purchases)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(purchases);

        if (card.Limit is not { } limit)
        {
            return null;
        }

        var used = CalculateUsedLimit(card.Id, today, purchases);
        return limit - used;
    }
}
