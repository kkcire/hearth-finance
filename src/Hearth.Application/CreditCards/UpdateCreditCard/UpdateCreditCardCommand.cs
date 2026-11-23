namespace Hearth.Application.CreditCards.UpdateCreditCard;

public sealed record UpdateCreditCardCommand(
    Guid CreditCardId,
    int ClosingDay,
    int DueDay,
    string? Name = null,
    decimal? Limit = null);
