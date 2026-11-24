using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Services;

/// <summary>
/// Pure calculations for credit card limit consumption.
/// Maintains exact symmetry with <see cref="RealBalanceCalculator"/>:
/// installments with <c>DueDate &lt;= today</c>, <c>DueDate &lt; initialBalanceDate</c>, or marked
/// as previously paid never occupy credit card limit; active installments with
/// <c>DueDate &gt; today</c> (and <c>&gt;= initialBalanceDate</c> when provided) occupy limit.
/// </summary>
public static class CreditCardLimitCalculator
{
    public static Money CalculateUsedLimit(
        Guid creditCardId,
        DateOnly today,
        IEnumerable<CreditCardPurchase> purchases,
        DateOnly? initialBalanceDate = null)
    {
        DomainValidation.EnsureNotEmpty(creditCardId, nameof(creditCardId), "credit card id");
        ArgumentNullException.ThrowIfNull(purchases);

        return Money.Sum(
            purchases
                .Where(purchase => purchase.CreditCardId == creditCardId)
                .SelectMany(purchase => purchase.GetActiveInstallments())
                .Where(installment => installment.Cycle.DueDate > today
                                      && (initialBalanceDate is null || installment.Cycle.DueDate >= initialBalanceDate.Value))
                .Select(installment => installment.Amount));
    }

    public static Money? CalculateAvailableLimit(
        CreditCard card,
        DateOnly today,
        IEnumerable<CreditCardPurchase> purchases,
        DateOnly? initialBalanceDate = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(purchases);

        if (card.Limit is not { } limit)
        {
            return null;
        }

        var used = CalculateUsedLimit(card.Id, today, purchases, initialBalanceDate);
        return limit - used;
    }
}
