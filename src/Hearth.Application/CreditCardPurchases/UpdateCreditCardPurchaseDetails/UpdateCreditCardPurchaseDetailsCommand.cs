namespace Hearth.Application.CreditCardPurchases.UpdateCreditCardPurchaseDetails;

public sealed record UpdateCreditCardPurchaseDetailsCommand(
    Guid PurchaseId,
    string Description,
    Guid CategoryId,
    Guid ConditionId);
