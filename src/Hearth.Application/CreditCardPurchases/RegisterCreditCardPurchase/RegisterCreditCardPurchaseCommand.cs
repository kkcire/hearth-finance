namespace Hearth.Application.CreditCardPurchases.RegisterCreditCardPurchase;

public sealed record RegisterCreditCardPurchaseCommand(
    Guid AccountId,
    Guid CreditCardId,
    DateOnly Date,
    decimal Amount,
    int InstallmentCount,
    int InitialInstallment,
    Guid ConditionId,
    string Description,
    Guid CategoryId);
