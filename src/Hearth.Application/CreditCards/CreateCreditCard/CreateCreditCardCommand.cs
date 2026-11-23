namespace Hearth.Application.CreditCards.CreateCreditCard;

public sealed record CreateCreditCardCommand(
    Guid AccountId,
    int ClosingDay,
    int DueDay,
    string? Name = null,
    decimal? Limit = null);
