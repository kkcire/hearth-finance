using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Invoices.GetInvoiceSummary;

public sealed record InvoiceInstallmentDto(
    Guid PurchaseId,
    Guid CreditCardId,
    DateOnly PurchaseDate,
    string Description,
    Guid CategoryId,
    PurchaseConditionKind ConditionKind,
    string? ConditionName,
    int InstallmentNumber,
    int InstallmentCount,
    decimal Amount,
    DateOnly ClosingDate,
    DateOnly DueDate);

public sealed record InvoiceSummaryDto(
    Guid AccountId,
    bool HasActiveCard,
    int ClosingYear,
    int ClosingMonth,
    int DefaultOpenClosingYear,
    int DefaultOpenClosingMonth,
    DateOnly? StartDate,
    DateOnly? EndDate,
    DateOnly? ClosingDate,
    DateOnly? DueDate,
    decimal CalculatedAmount,
    InvoiceStatus? Status,
    IReadOnlyList<InvoiceInstallmentDto> Installments)
{
    /// <summary>
    /// True only when the account has no active credit card AND no installments from
    /// archived cards in the requested closing month.
    /// </summary>
    public bool IsEmptyWithoutCard => !HasActiveCard && Installments.Count == 0;
}
