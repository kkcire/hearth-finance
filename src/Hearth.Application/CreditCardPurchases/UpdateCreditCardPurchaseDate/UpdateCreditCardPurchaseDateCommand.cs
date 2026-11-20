namespace Hearth.Application.CreditCardPurchases.UpdateCreditCardPurchaseDate;

public sealed record UpdateCreditCardPurchaseDateCommand(Guid PurchaseId, DateOnly NewDate);
