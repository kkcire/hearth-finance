using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Hearth.Infrastructure.Persistence.Repositories;

internal sealed class InvoiceRepository : IInvoiceRepository
{
    private readonly HearthDbContext _context;

    public InvoiceRepository(HearthDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        await _context.Invoices.AddAsync(invoice, cancellationToken);
    }

    public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Invoices.FirstOrDefaultAsync(invoice => invoice.Id == id, cancellationToken);

    public Task<Invoice?> GetByAccountAndClosingMonthAsync(
        Guid accountId,
        YearMonth closingMonth,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(closingMonth);

        return _context.Invoices.FirstOrDefaultAsync(
            invoice => invoice.AccountId == accountId && invoice.Cycle.ClosingMonth == closingMonth,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Invoice>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default) =>
        await _context.Invoices
            .Where(invoice => invoice.AccountId == accountId)
            .ToListAsync(cancellationToken);

    public Task UpdateAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        if (_context.Entry(invoice).State == EntityState.Detached)
        {
            _context.Invoices.Update(invoice);
        }

        return Task.CompletedTask;
    }
}
