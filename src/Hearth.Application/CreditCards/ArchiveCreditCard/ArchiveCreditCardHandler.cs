using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.CreditCards.ArchiveCreditCard;

public sealed class ArchiveCreditCardHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly ICreditCardRepository _creditCards;
    private readonly IUnitOfWork _unitOfWork;

    public ArchiveCreditCardHandler(
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
        ArchiveCreditCardCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _accountAccess.GetRequiredUserId();

        var card = await _creditCards.GetByIdAsync(command.CreditCardId, cancellationToken)
                   ?? throw new NotFoundException(nameof(CreditCard), command.CreditCardId);

        await _accountAccess.EnsureCanAccessEntityAsync(
            card.AccountId, nameof(CreditCard), card.Id, cancellationToken);

        var changed = card.Archive();

        if (changed)
        {
            await _creditCards.UpdateAsync(card, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }
}
