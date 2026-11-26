namespace Hearth.Domain.ValueObjects;

/// <summary>
/// Summary of the relevant credit card billing cycle together with
/// all future active installments committed across subsequent cycles.
/// </summary>
public sealed record CurrentInvoiceSummary(
    YearMonth BillingMonth,
    Money CycleAmount,
    DateOnly? DueDate,
    DateOnly? ClosingDate,
    InvoiceStatus? Status,
    Money FutureInstallmentsTotal,
    bool HasActiveCard);
