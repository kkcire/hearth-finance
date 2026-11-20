using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.Invoices.ListInvoices;

public sealed class ListInvoicesHandler
{
    private readonly IAccountRepository _accounts;
    private readonly IInvoiceRepository _invoices;

    public ListInvoicesHandler(IAccountRepository accounts, IInvoiceRepository invoices)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(invoices);

        _accounts = accounts;
        _invoices = invoices;
    }

    public async Task<IReadOnlyList<InvoiceDto>> ExecuteAsync(
        ListInvoicesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        _ = await _accounts.GetByIdAsync(query.AccountId, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), query.AccountId);

        var invoices = await _invoices.GetByAccountAsync(query.AccountId, cancellationToken);

        return invoices
            .OrderByDescending(invoice => invoice.Cycle.ClosingMonth)
            .Select(InvoiceDto.FromEntity)
            .ToList();
    }
}
