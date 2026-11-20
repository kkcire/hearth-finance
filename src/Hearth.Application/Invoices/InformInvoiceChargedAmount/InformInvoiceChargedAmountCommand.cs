namespace Hearth.Application.Invoices.InformInvoiceChargedAmount;

public sealed record InformInvoiceChargedAmountCommand(
    Guid AccountId,
    int ClosingYear,
    int ClosingMonth,
    decimal ChargedAmount);
