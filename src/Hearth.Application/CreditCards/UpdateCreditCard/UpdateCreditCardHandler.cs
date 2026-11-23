using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.CreditCards.UpdateCreditCard;

public sealed class UpdateCreditCardHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly ICreditCardRepository _creditCards;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCreditCardHandler(
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

    public async Task ExecuteAsync(
        UpdateCreditCardCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _accountAccess.GetRequiredUserId();
        var billingRule = CardBillingRule.Of(command.ClosingDay, command.DueDay);
        Money? limit = command.Limit.HasValue ? Money.FromDecimal(command.Limit.Value) : null;

        var card = await _creditCards.GetByIdAsync(command.CreditCardId, cancellationToken)
                   ?? throw new NotFoundException(nameof(CreditCard), command.CreditCardId);

        await _accountAccess.EnsureCanAccessEntityAsync(
            card.AccountId, nameof(CreditCard), card.Id, cancellationToken);

        card.UpdateDetails(billingRule, command.Name, limit);

        await _creditCards.UpdateAsync(card, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
