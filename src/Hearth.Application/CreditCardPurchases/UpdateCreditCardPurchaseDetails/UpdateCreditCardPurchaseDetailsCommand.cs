using Hearth.Domain.ValueObjects;

namespace Hearth.Application.CreditCardPurchases.UpdateCreditCardPurchaseDetails;

public sealed record UpdateCreditCardPurchaseDetailsCommand(
    Guid PurchaseId,
    string Description,
    ExpenseCategory Category,
    PurchaseConditionKind ConditionKind,
    string? ConditionName);
