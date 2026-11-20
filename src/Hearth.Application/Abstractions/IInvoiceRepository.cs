using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Abstractions;

public interface IInvoiceRepository
{
    Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default);

    Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Invoice?> GetByAccountAndClosingMonthAsync(
        Guid accountId,
        YearMonth closingMonth,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Invoice>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(Invoice invoice, CancellationToken cancellationToken = default);
}
