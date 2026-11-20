namespace Hearth.Application.Invoices.GetInvoiceSummary;

public sealed record GetInvoiceSummaryQuery(Guid AccountId, int ClosingYear, int ClosingMonth);
