using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.CreditCards.CreateCreditCard;

public sealed class CreateCreditCardHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly ICreditCardRepository _creditCards;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCreditCardHandler(
        AccountAccessGuard accountAccess,
        ICreditCardRepository creditCards,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(creditCards);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _creditCards = creditCards;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> ExecuteAsync(
        CreateCreditCardCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var billingRule = CardBillingRule.Of(command.ClosingDay, command.DueDay);
        Money? limit = command.Limit.HasValue ? Money.FromDecimal(command.Limit.Value) : null;

        var account = await _accountAccess.GetAuthorizedAccountAsync(command.AccountId, cancellationToken);

        var existingActive = await _creditCards.GetActiveByAccountAsync(account.Id, cancellationToken);
        if (existingActive is not null)
        {
            throw new DomainException(
                "This account already has an active credit card. Archive or delete it before adding another.");
        }

        var card = CreditCard.Create(
            Guid.CreateVersion7(),
            account.Id,
            billingRule,
            command.Name,
            limit);

        await _creditCards.AddAsync(card, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return card.Id;
    }
}
