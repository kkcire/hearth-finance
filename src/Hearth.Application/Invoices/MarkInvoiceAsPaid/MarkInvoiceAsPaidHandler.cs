using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.Invoices.MarkInvoiceAsPaid;

public sealed class MarkInvoiceAsPaidHandler
{
    private readonly IAccountRepository _accounts;
    private readonly IInvoiceRepository _invoices;
    private readonly IUnitOfWork _unitOfWork;

    public MarkInvoiceAsPaidHandler(
        IAccountRepository accounts,
        IInvoiceRepository invoices,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(invoices);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accounts = accounts;
        _invoices = invoices;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> ExecuteAsync(
        MarkInvoiceAsPaidCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var invoice = await _invoices.GetByIdAsync(command.InvoiceId, cancellationToken)
                      ?? throw new NotFoundException(nameof(Invoice), command.InvoiceId);

        var account = await _accounts.GetByIdAsync(invoice.AccountId, cancellationToken)
                      ?? throw new NotFoundException(nameof(Account), invoice.AccountId);

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
