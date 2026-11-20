using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Tests.Application.Fakes;

internal sealed class InMemoryInvoiceRepository : IInvoiceRepository
{
    private readonly Dictionary<Guid, Invoice> _invoices = new();

    public IReadOnlyCollection<Invoice> Invoices => _invoices.Values;

    public int AddCount { get; private set; }

    public int UpdateCount { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _invoices.Add(invoice.Id, invoice);
        AddCount++;
        return Task.CompletedTask;
    }

    public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        return Task.FromResult(_invoices.GetValueOrDefault(id));
    }

    public Task<Invoice?> GetByAccountAndClosingMonthAsync(
        Guid accountId,
        YearMonth closingMonth,
        CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        var invoice = _invoices.Values.FirstOrDefault(
            item => item.AccountId == accountId && item.Cycle.ClosingMonth == closingMonth);
        return Task.FromResult(invoice);
    }

    public Task<IReadOnlyList<Invoice>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        IReadOnlyList<Invoice> result = _invoices.Values
            .Where(invoice => invoice.AccountId == accountId)
            .ToList();
        return Task.FromResult(result);
    }

    public Task UpdateAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _invoices[invoice.Id] = invoice;
        UpdateCount++;
        return Task.CompletedTask;
    }
}
