using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.Invoices.RevertInvoicePayment;

public sealed class RevertInvoicePaymentHandler
{
    private readonly IInvoiceRepository _invoices;
    private readonly IUnitOfWork _unitOfWork;

    public RevertInvoicePaymentHandler(IInvoiceRepository invoices, IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(invoices);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _invoices = invoices;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> ExecuteAsync(
        RevertInvoicePaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var invoice = await _invoices.GetByIdAsync(command.InvoiceId, cancellationToken)
                      ?? throw new NotFoundException(nameof(Invoice), command.InvoiceId);

        var changed = invoice.RevertPayment();

        if (changed)
        {
            await _invoices.UpdateAsync(invoice, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }
}
