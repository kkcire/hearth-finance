using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Invoices.InformInvoiceChargedAmount;

public sealed class InformInvoiceChargedAmountHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IInvoiceRepository _invoices;
    private readonly IUnitOfWork _unitOfWork;

    public InformInvoiceChargedAmountHandler(
        AccountAccessGuard accountAccess,
        IInvoiceRepository invoices,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(invoices);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _invoices = invoices;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> ExecuteAsync(
        InformInvoiceChargedAmountCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var closingMonth = YearMonth.Of(command.ClosingYear, command.ClosingMonth);
        var chargedAmount = Money.FromDecimal(command.ChargedAmount);

        var account = await _accountAccess.GetAuthorizedAccountAsync(command.AccountId, cancellationToken);

        var existing = await _invoices.GetByAccountAndClosingMonthAsync(
            account.Id, closingMonth, cancellationToken);

        if (existing is null)
        {
            var cycle = BillingCycle.Closing(closingMonth, account.BillingRule);
            var invoice = Invoice.Create(Guid.CreateVersion7(), account.Id, cycle);
            invoice.InformChargedAmount(chargedAmount);

            await _invoices.AddAsync(invoice, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return invoice.Id;
        }

        existing.InformChargedAmount(chargedAmount);

        await _invoices.UpdateAsync(existing, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return existing.Id;
    }
}
