using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.CreditCardPurchases.UpdateCreditCardPurchaseDetails;

public sealed class UpdateCreditCardPurchaseDetailsHandler
{
    private readonly ICreditCardPurchaseRepository _purchases;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCreditCardPurchaseDetailsHandler(
        ICreditCardPurchaseRepository purchases,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(purchases);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _purchases = purchases;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        UpdateCreditCardPurchaseDetailsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var condition = PurchaseConditionMapper.ToDomain(command.ConditionKind, command.ConditionName);

        var purchase = await _purchases.GetByIdAsync(command.PurchaseId, cancellationToken)
                       ?? throw new NotFoundException(nameof(CreditCardPurchase), command.PurchaseId);

        purchase.UpdateDescription(command.Description);
        purchase.UpdateCategory(command.Category);
        purchase.UpdateCondition(condition);

        await _purchases.UpdateAsync(purchase, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
