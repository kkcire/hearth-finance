using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.CreditCardPurchases.UpdateCreditCardPurchaseAmount;

public sealed class UpdateCreditCardPurchaseAmountHandler
{
    private readonly ICreditCardPurchaseRepository _purchases;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCreditCardPurchaseAmountHandler(
        ICreditCardPurchaseRepository purchases,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(purchases);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _purchases = purchases;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        UpdateCreditCardPurchaseAmountCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var newAmount = Money.FromDecimal(command.NewAmount);

        var purchase = await _purchases.GetByIdAsync(command.PurchaseId, cancellationToken)
                       ?? throw new NotFoundException(nameof(CreditCardPurchase), command.PurchaseId);

        purchase.UpdateAmount(newAmount, _clock.Today);

        await _purchases.UpdateAsync(purchase, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
