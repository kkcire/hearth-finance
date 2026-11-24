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
    /// never count. When <paramref name="conditionId"/> is provided, only purchases matching that
    /// condition are included; when <see langword="null"/>, all conditions are summed.
    /// </summary>
    /// <returns>The calculated amount, or zero when no installment falls in the cycle.</returns>
    public static Money Calculate(
        Guid accountId,
        YearMonth closingMonth,
        IEnumerable<CreditCardPurchase> purchases,
        Guid? conditionId = null)
    {
        ArgumentNullException.ThrowIfNull(closingMonth);
        return SumInstallments(accountId, purchases, conditionId, cycle => cycle.ClosingMonth == closingMonth);
    }

    /// <summary>
    /// Sums the active installments of the account's purchases whose <see cref="BillingCycle.DueMonth"/>
    /// falls in <paramref name="dueMonth"/>. When <paramref name="conditionId"/> is provided, only
    /// purchases matching that condition are included.
    /// </summary>
    public static Money CalculateByDueMonth(
        Guid accountId,
        YearMonth dueMonth,
        IEnumerable<CreditCardPurchase> purchases,
        Guid? conditionId = null)
    {
        ArgumentNullException.ThrowIfNull(dueMonth);
        return SumInstallments(accountId, purchases, conditionId, cycle => cycle.DueMonth == dueMonth);
    }

    private static Money SumInstallments(
        Guid accountId,
        IEnumerable<CreditCardPurchase> purchases,
        Guid? conditionId,
        Func<BillingCycle, bool> cyclePredicate)
    {
        DomainValidation.EnsureNotEmpty(accountId, nameof(accountId), "account id");
        ArgumentNullException.ThrowIfNull(purchases);
        DomainValidation.EnsureOptionalNotEmpty(conditionId, nameof(conditionId), "condition id");

        return Money.Sum(
            purchases
                .Where(purchase => purchase.AccountId == accountId
                                   && (!conditionId.HasValue || purchase.ConditionId == conditionId.Value))
                .SelectMany(purchase => purchase.GetActiveInstallments())
                .Where(installment => cyclePredicate(installment.Cycle))
                .Select(installment => installment.Amount));
    }
}