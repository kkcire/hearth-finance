using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.CreditCardPurchases;

public sealed record InstallmentDto(
    int Number,
    decimal Amount,
    int ClosingYear,
    int ClosingMonth,
    DateOnly DueDate,
    bool IsPreviouslyPaid);

public sealed record CreditCardPurchaseDto(
    Guid Id,
    Guid AccountId,
    DateOnly Date,
    decimal Amount,
    int InstallmentCount,
    int InitialInstallment,
    PurchaseConditionKind ConditionKind,
    string? ConditionName,
    string Description,
    Guid CategoryId,
    bool IsLocked,
    IReadOnlyList<InstallmentDto> Installments)
{
    internal static CreditCardPurchaseDto FromEntity(CreditCardPurchase purchase, DateOnly today) =>
        new(
            purchase.Id,
            purchase.AccountId,
            purchase.Date,
            purchase.Amount.Amount,
            purchase.InstallmentCount,
            purchase.InitialInstallment,
            purchase.Condition.Kind,
            purchase.Condition.Name,
            purchase.Description,
            purchase.CategoryId,
            purchase.IsLockedAt(today),
            purchase.GetInstallments()
                .Select(installment => new InstallmentDto(
                    installment.Number,
                    installment.Amount.Amount,
                    installment.Cycle.ClosingMonth.Year,
                    installment.Cycle.ClosingMonth.Month,
                    installment.Cycle.DueDate,
                    installment.IsPreviouslyPaid))
                .ToList());
}

internal static class PurchaseConditionMapper
{
    public static PurchaseCondition ToDomain(PurchaseConditionKind kind, string? name) =>
        kind switch
        {
            PurchaseConditionKind.Own => EnsureNoName(PurchaseCondition.Own, name),
            PurchaseConditionKind.Others => EnsureNoName(PurchaseCondition.Others, name),
            PurchaseConditionKind.Custom => PurchaseCondition.Custom(name!),
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind), kind, "The purchase condition kind is not known."),
        };

    private static PurchaseCondition EnsureNoName(PurchaseCondition condition, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "A condition name can only be provided when the kind is Custom.", nameof(name));
        }

        return condition;
    }
}
