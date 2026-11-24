using Hearth.Domain.Entities;

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
    Guid CreditCardId,
    DateOnly Date,
    decimal Amount,
    int InstallmentCount,
    int InitialInstallment,
    int FirstActiveClosingYear,
    int FirstActiveClosingMonth,
    Guid ConditionId,
    string Description,
    Guid CategoryId,
    bool IsLocked,
    IReadOnlyList<InstallmentDto> Installments)
{
    internal static CreditCardPurchaseDto FromEntity(CreditCardPurchase purchase, DateOnly today)
    {
        var firstActive = purchase.FirstActiveCycle.ClosingMonth;

        return new(
            purchase.Id,
            purchase.AccountId,
            purchase.CreditCardId,
            purchase.Date,
            purchase.Amount.Amount,
            purchase.InstallmentCount,
            purchase.InitialInstallment,
            firstActive.Year,
            firstActive.Month,
            purchase.ConditionId,
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
}
