namespace Hearth.Application.CreditCardPurchases.UpdateCreditCardPurchaseAmount;

public sealed record UpdateCreditCardPurchaseAmountCommand(Guid PurchaseId, decimal NewAmount);
