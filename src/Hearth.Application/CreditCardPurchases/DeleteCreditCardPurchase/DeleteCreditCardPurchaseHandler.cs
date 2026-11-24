using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;

namespace Hearth.Application.CreditCardPurchases.DeleteCreditCardPurchase;

public sealed class DeleteCreditCardPurchaseHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly ICreditCardPurchaseRepository _purchases;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCreditCardPurchaseHandler(
        AccountAccessGuard accountAccess,
        ICreditCardPurchaseRepository purchases,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(purchases);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _purchases = purchases;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        DeleteCreditCardPurchaseCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _accountAccess.GetRequiredUserId();

        var purchase = await _purchases.GetByIdAsync(command.PurchaseId, cancellationToken)
                       ?? throw new NotFoundException(nameof(CreditCardPurchase), command.PurchaseId);

        await _accountAccess.EnsureCanAccessEntityAsync(
            purchase.AccountId, nameof(CreditCardPurchase), purchase.Id, cancellationToken);

        if (purchase.IsLockedAt(_clock.Today))
        {
            throw new DomainException(
                "The purchase cannot be deleted after a linked invoice has been paid.");
        }

        await _purchases.DeleteAsync(purchase, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
