using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Invoices.GetInvoiceSummary;

public sealed record InvoiceInstallmentDto(
    Guid PurchaseId,
    DateOnly PurchaseDate,
    string Description,
    ExpenseCategory Category,
    PurchaseConditionKind ConditionKind,
    string? ConditionName,
    int InstallmentNumber,
    int InstallmentCount,
    decimal Amount);

public sealed record InvoiceSummaryDto(
    Guid AccountId,
    Guid? InvoiceId,
    int ClosingYear,
    int ClosingMonth,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly ClosingDate,
    DateOnly DueDate,
    decimal CalculatedAmount,
    decimal? ChargedAmount,
    decimal? Divergence,
    DateOnly? PaidOn,
    bool IsPaid,
    IReadOnlyList<InvoiceInstallmentDto> Installments);
