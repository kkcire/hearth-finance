namespace Hearth.Application.CreditCards.ListCreditCards;

public sealed record ListCreditCardsQuery(Guid AccountId, bool IncludeArchived = false);
