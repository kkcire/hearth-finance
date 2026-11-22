using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.Invoices.MarkInvoiceAsPaid;

public sealed class MarkInvoiceAsPaidHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IInvoiceRepository _invoices;
    private readonly IUnitOfWork _unitOfWork;

    public MarkInvoiceAsPaidHandler(
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

    public async Task<bool> ExecuteAsync(
        MarkInvoiceAsPaidCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _accountAccess.GetRequiredUserId();

        var invoice = await _invoices.GetByIdAsync(command.InvoiceId, cancellationToken)
                      ?? throw new NotFoundException(nameof(Invoice), command.InvoiceId);

        var account = await _accountAccess.EnsureCanAccessEntityAsync(
            invoice.AccountId, nameof(Invoice), invoice.Id, cancellationToken);

        account.EnsureCanRegisterOn(command.PaidOn);

        var changed = invoice.MarkAsPaid(command.PaidOn);

        if (changed)
        {
            await _invoices.UpdateAsync(invoice, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }
}
