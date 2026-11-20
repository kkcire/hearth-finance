using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Invoices.InformInvoiceChargedAmount;

public sealed class InformInvoiceChargedAmountHandler
{
    private readonly IAccountRepository _accounts;
    private readonly IInvoiceRepository _invoices;
    private readonly IUnitOfWork _unitOfWork;

    public InformInvoiceChargedAmountHandler(
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

    public async Task<Guid> ExecuteAsync(
        InformInvoiceChargedAmountCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var closingMonth = YearMonth.Of(command.ClosingYear, command.ClosingMonth);
        var chargedAmount = Money.FromDecimal(command.ChargedAmount);

        var account = await _accounts.GetByIdAsync(command.AccountId, cancellationToken)
                      ?? throw new NotFoundException(nameof(Account), command.AccountId);

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
