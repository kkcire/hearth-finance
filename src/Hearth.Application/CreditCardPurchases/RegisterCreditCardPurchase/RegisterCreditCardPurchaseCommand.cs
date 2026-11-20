using Hearth.Domain.ValueObjects;

namespace Hearth.Application.CreditCardPurchases.RegisterCreditCardPurchase;

public sealed record RegisterCreditCardPurchaseCommand(
    Guid AccountId,
    DateOnly Date,
    decimal Amount,
    int InstallmentCount,
    int InitialInstallment,
    PurchaseConditionKind ConditionKind,
    string? ConditionName,
    string Description,
    ExpenseCategory Category);
