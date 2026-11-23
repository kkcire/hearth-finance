using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;

namespace Hearth.Application.CreditCards.DeleteCreditCard;

public sealed class DeleteCreditCardHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly ICreditCardRepository _creditCards;
    private readonly ICreditCardPurchaseRepository _purchases;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCreditCardHandler(
        AccountAccessGuard accountAccess,
        ICreditCardRepository creditCards,
        ICreditCardPurchaseRepository purchases,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(creditCards);
        ArgumentNullException.ThrowIfNull(purchases);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _creditCards = creditCards;
        _purchases = purchases;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        DeleteCreditCardCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _accountAccess.GetRequiredUserId();

        var card = await _creditCards.GetByIdAsync(command.CreditCardId, cancellationToken)
                   ?? throw new NotFoundException(nameof(CreditCard), command.CreditCardId);

        await _accountAccess.EnsureCanAccessEntityAsync(
            card.AccountId, nameof(CreditCard), card.Id, cancellationToken);

        if (await _purchases.AnyByCreditCardAsync(card.Id, cancellationToken))
        {
            throw new DomainException(
                "A credit card that has purchases cannot be deleted. Archive it instead.");
        }

        await _creditCards.DeleteAsync(card, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
