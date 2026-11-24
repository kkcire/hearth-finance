namespace Hearth.Application.Invoices.GetInvoiceSummary;

public enum InvoiceMonthBasis
{
    ClosingMonth = 0,
    DueMonth = 1,
}

public sealed record GetInvoiceSummaryQuery(
    Guid AccountId,
    int ClosingYear,
    int ClosingMonth,
    Guid? ConditionId = null,
    InvoiceMonthBasis MonthBasis = InvoiceMonthBasis.ClosingMonth);
