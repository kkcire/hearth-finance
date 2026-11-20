using Hearth.Domain.Entities;

namespace Hearth.Application.Invoices;

public sealed record InvoiceDto(
    Guid Id,
    Guid AccountId,
    int ClosingYear,
    int ClosingMonth,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly ClosingDate,
    DateOnly DueDate,
    decimal? ChargedAmount,
    DateOnly? PaidOn,
    bool IsPaid)
{
    internal static InvoiceDto FromEntity(Invoice invoice) =>
        new(
            invoice.Id,
            invoice.AccountId,
            invoice.Cycle.ClosingMonth.Year,
            invoice.Cycle.ClosingMonth.Month,
            invoice.Cycle.StartDate,
            invoice.Cycle.EndDate,
            invoice.Cycle.ClosingDate,
            invoice.Cycle.DueDate,
            invoice.ChargedAmount?.Amount,
            invoice.PaidOn,
            invoice.IsPaid);
}
