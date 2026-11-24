using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.PurchaseConditions;
using Hearth.Domain.Services;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Invoices.GetInvoiceSummary;

public sealed class GetInvoiceSummaryHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly ICreditCardRepository _creditCards;
    private readonly ICreditCardPurchaseRepository _purchases;
    private readonly IClock _clock;

    public GetInvoiceSummaryHandler(
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

    public async Task<InvoiceSummaryDto> ExecuteAsync(
        GetInvoiceSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        PurchaseConditionValidation.EnsureOptionalFilterNotEmpty(query.ConditionId, nameof(query));

        var targetMonth = YearMonth.Of(query.ClosingYear, query.ClosingMonth);
        var today = _clock.Today;

        var account = await _accountAccess.GetAuthorizedAccountAsync(query.AccountId, cancellationToken);
        var activeCard = await _creditCards.GetActiveByAccountAsync(account.Id, cancellationToken);
        var purchases = await _purchases.GetByAccountAsync(account.Id, cancellationToken);

        var groupByDueMonth = query.MonthBasis == InvoiceMonthBasis.DueMonth;
        var calculatedAmount = groupByDueMonth
            ? InvoiceCalculator.CalculateByDueMonth(account.Id, targetMonth, purchases, query.ConditionId)
            : InvoiceCalculator.Calculate(account.Id, targetMonth, purchases, query.ConditionId);

        var matchingInstallments = purchases
            .Where(purchase => purchase.AccountId == account.Id
                               && (!query.ConditionId.HasValue || purchase.ConditionId == query.ConditionId.Value))
            .SelectMany(purchase => purchase.GetInstallments()
                .Where(installment => !installment.IsPreviouslyPaid
                                      && (groupByDueMonth
                                          ? installment.Cycle.DueMonth == targetMonth
                                          : installment.Cycle.ClosingMonth == targetMonth))
                .Select(installment => (Purchase: purchase, Installment: installment)))
            .ToList();

        var installmentDtos = matchingInstallments
            .Select(item => new InvoiceInstallmentDto(
                item.Purchase.Id,
                item.Purchase.CreditCardId,
                item.Purchase.Date,
                item.Purchase.Description,
                item.Purchase.CategoryId,
                item.Purchase.ConditionId,
                item.Installment.Number,
                item.Purchase.InstallmentCount,
                item.Installment.Amount.Amount,
                item.Installment.Cycle.ClosingDate,
                item.Installment.Cycle.DueDate))
            .OrderBy(item => item.PurchaseDate)
            .ThenBy(item => item.Description, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.InstallmentNumber)
            .ToList();

        var defaultOpenMonth = activeCard is not null
            ? (groupByDueMonth
                ? BillingCycle.ForPurchase(today, activeCard.BillingRule).DueMonth
                : BillingCycle.ForPurchase(today, activeCard.BillingRule).ClosingMonth)
            : YearMonth.From(today);

        BillingCycle? cycle = activeCard is not null
            ? (groupByDueMonth
                ? BillingCycle.DueIn(targetMonth, activeCard.BillingRule)
                : BillingCycle.Closing(targetMonth, activeCard.BillingRule))
            : matchingInstallments.FirstOrDefault().Installment?.Cycle;

        InvoiceStatus? status = cycle is not null
            ? cycle.GetStatus(today)
            : null;

        return new InvoiceSummaryDto(
            account.Id,
            activeCard is not null,
            cycle?.ClosingMonth.Year ?? targetMonth.Year,
            cycle?.ClosingMonth.Month ?? targetMonth.Month,
            defaultOpenMonth.Year,
            defaultOpenMonth.Month,
            cycle?.StartDate,
            cycle?.EndDate,
            cycle?.ClosingDate,
            cycle?.DueDate,
            calculatedAmount.Amount,
            status,
            installmentDtos,
            query.ConditionId);
    }
}
