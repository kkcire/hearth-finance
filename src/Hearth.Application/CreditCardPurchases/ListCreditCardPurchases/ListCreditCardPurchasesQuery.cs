namespace Hearth.Application.CreditCardPurchases.ListCreditCardPurchases;

public sealed record ListCreditCardPurchasesQuery(Guid AccountId, Guid? ConditionId = null);
