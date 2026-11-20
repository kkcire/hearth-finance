namespace Hearth.Application.Invoices.MarkInvoiceAsPaid;

public sealed record MarkInvoiceAsPaidCommand(Guid InvoiceId, DateOnly PaidOn);
