using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;

namespace Hearth.Application.Invoices.ListInvoices;

public sealed class ListInvoicesHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IInvoiceRepository _invoices;

    public ListInvoicesHandler(AccountAccessGuard accountAccess, IInvoiceRepository invoices)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(invoices);

        _accountAccess = accountAccess;
        _invoices = invoices;
    }

    public async Task<IReadOnlyList<InvoiceDto>> ExecuteAsync(
        ListInvoicesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        _ = await _accountAccess.GetAuthorizedAccountAsync(query.AccountId, cancellationToken);

        var invoices = await _invoices.GetByAccountAsync(query.AccountId, cancellationToken);

        return invoices
            .OrderByDescending(invoice => invoice.Cycle.ClosingMonth)
            .Select(InvoiceDto.FromEntity)
            .ToList();
    }
}
