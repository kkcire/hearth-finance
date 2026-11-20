using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Services;

/// <summary>
/// Calculates the invoice of a cycle from the credit card purchases. The invoice is never stored:
/// it is always the sum of the installments that belong to the cycle.
/// </summary>
public static class InvoiceCalculator
{
    /// <summary>
    /// Sums the installments of the account's purchases that fall in the cycle closing in the given
    /// month. Installments before the initial one of a purchase in progress are considered paid and
    /// never count. Direct expenses and fixed expenses are not part of the invoice, and the purchase
    /// condition (own, others, custom) is only a label, so none of them changes the result.
    /// </summary>
    /// <returns>The calculated amount, or zero when no installment falls in the cycle.</returns>
    public static Money Calculate(
        Guid accountId,
        YearMonth closingMonth,
        IEnumerable<CreditCardPurchase> purchases)
    {
        DomainValidation.EnsureNotEmpty(accountId, nameof(accountId), "account id");
        ArgumentNullException.ThrowIfNull(closingMonth);
        ArgumentNullException.ThrowIfNull(purchases);

        // A cycle is identified by its closing month. Matching on the month alone, not on the whole
        // cycle, keeps purchases created with a previous closing day on the invoice they were given.
        return Money.Sum(
            purchases
                .Where(purchase => purchase.AccountId == accountId)
                .SelectMany(purchase => purchase.GetInstallments())
                .Where(installment => !installment.IsPreviouslyPaid
                                      && installment.Cycle.ClosingMonth == closingMonth)
                .Select(installment => installment.Amount));
    }
}