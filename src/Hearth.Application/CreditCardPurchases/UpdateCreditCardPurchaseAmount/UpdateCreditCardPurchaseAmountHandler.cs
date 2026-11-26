using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.CreditCardPurchases.UpdateCreditCardPurchaseAmount;

public sealed class UpdateCreditCardPurchaseAmountHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly ICreditCardPurchaseRepository _purchases;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCreditCardPurchaseAmountHandler(
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
        UpdateCreditCardPurchaseAmountCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _accountAccess.GetRequiredUserId();
        var newAmount = Money.FromDecimal(command.NewAmount);

        var purchase = await _purchases.GetByIdAsync(command.PurchaseId, cancellationToken)
                       ?? throw new NotFoundException(nameof(CreditCardPurchase), command.PurchaseId);

        var account = await _accountAccess.EnsureCanAccessEntityAsync(
            purchase.AccountId, nameof(CreditCardPurchase), purchase.Id, cancellationToken);

        purchase.UpdateAmount(newAmount, _clock.Today, account.TrackingStartDate);

        await _purchases.UpdateAsync(purchase, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
