using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;

namespace Hearth.Application.CreditCards.RestoreCreditCard;

public sealed class RestoreCreditCardHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly ICreditCardRepository _creditCards;
    private readonly IUnitOfWork _unitOfWork;

    public RestoreCreditCardHandler(
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

    public async Task<bool> ExecuteAsync(
        RestoreCreditCardCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _accountAccess.GetRequiredUserId();

        var card = await _creditCards.GetByIdAsync(command.CreditCardId, cancellationToken)
                   ?? throw new NotFoundException(nameof(CreditCard), command.CreditCardId);

        await _accountAccess.EnsureCanAccessEntityAsync(
            card.AccountId, nameof(CreditCard), card.Id, cancellationToken);

        if (!card.IsArchived)
        {
            return false;
        }

        var activeCard = await _creditCards.GetActiveByAccountAsync(card.AccountId, cancellationToken);
        if (activeCard is not null && activeCard.Id != card.Id)
        {
            throw new DomainException(
                "This account already has an active credit card. Archive or delete it before restoring another card.");
        }

        var changed = card.Restore();

        if (changed)
        {
            await _creditCards.UpdateAsync(card, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }
}
