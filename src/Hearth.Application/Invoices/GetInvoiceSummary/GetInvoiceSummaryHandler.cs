using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Domain.Services;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Invoices.GetInvoiceSummary;

public sealed class GetInvoiceSummaryHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IInvoiceRepository _invoices;
    private readonly ICreditCardPurchaseRepository _purchases;

    public GetInvoiceSummaryHandler(
        AccountAccessGuard accountAccess,
        IInvoiceRepository invoices,
        ICreditCardPurchaseRepository purchases)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(invoices);
        ArgumentNullException.ThrowIfNull(purchases);

        _accountAccess = accountAccess;
        _invoices = invoices;
        _purchases = purchases;
    }

    public async Task<InvoiceSummaryDto> ExecuteAsync(
        GetInvoiceSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var closingMonth = YearMonth.Of(query.ClosingYear, query.ClosingMonth);

        var account = await _accountAccess.GetAuthorizedAccountAsync(query.AccountId, cancellationToken);

        var invoice = await _invoices.GetByAccountAndClosingMonthAsync(
            account.Id, closingMonth, cancellationToken);
        var purchases = await _purchases.GetByAccountAsync(account.Id, cancellationToken);

        var cycle = invoice?.Cycle ?? BillingCycle.Closing(closingMonth, account.BillingRule);
        var calculatedAmount = InvoiceCalculator.Calculate(account.Id, closingMonth, purchases);
        var divergence = invoice?.DivergenceFrom(calculatedAmount);

        var installments = purchases
            .Where(purchase => purchase.AccountId == account.Id)
            .SelectMany(purchase => purchase.GetInstallments()
                .Where(installment => !installment.IsPreviouslyPaid
                                      && installment.Cycle.ClosingMonth == closingMonth)
                .Select(installment => new InvoiceInstallmentDto(
                    purchase.Id,
                    purchase.Date,
                    purchase.Description,
                    purchase.CategoryId,
                    purchase.Condition.Kind,
                    purchase.Condition.Name,
                    installment.Number,
                    purchase.InstallmentCount,
                    installment.Amount.Amount)))
            .OrderBy(item => item.PurchaseDate)
            .ThenBy(item => item.Description, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.InstallmentNumber)
            .ToList();

        return new InvoiceSummaryDto(
            account.Id,
            invoice?.Id,
            cycle.ClosingMonth.Year,
            cycle.ClosingMonth.Month,
            cycle.StartDate,
            cycle.EndDate,
            cycle.ClosingDate,
            cycle.DueDate,
            calculatedAmount.Amount,
            invoice?.ChargedAmount?.Amount,
            divergence?.Amount,
            invoice?.PaidOn,
            invoice?.IsPaid ?? false,
            installments);
    }
}
