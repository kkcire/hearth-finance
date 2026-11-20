using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;

namespace Hearth.Application.CreditCardPurchases.DeleteCreditCardPurchase;

public sealed class DeleteCreditCardPurchaseHandler
{
    private readonly ICreditCardPurchaseRepository _purchases;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCreditCardPurchaseHandler(
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
        DeleteCreditCardPurchaseCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var purchase = await _purchases.GetByIdAsync(command.PurchaseId, cancellationToken)
                       ?? throw new NotFoundException(nameof(CreditCardPurchase), command.PurchaseId);

        if (purchase.IsLockedAt(_clock.Today))
        {
            throw new DomainException(
                "The purchase cannot be deleted after its first counting invoice has closed.");
        }

        await _purchases.DeleteAsync(purchase, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
