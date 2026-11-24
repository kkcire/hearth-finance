using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Categories;
using Hearth.Application.Exceptions;
using Hearth.Application.PurchaseConditions;
using Hearth.Domain.Entities;

namespace Hearth.Application.CreditCardPurchases.UpdateCreditCardPurchaseDetails;

public sealed class UpdateCreditCardPurchaseDetailsHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly ICategoryRepository _categories;
    private readonly IPurchaseConditionRepository _conditions;
    private readonly ICreditCardPurchaseRepository _purchases;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCreditCardPurchaseDetailsHandler(
        AccountAccessGuard accountAccess,
        ICategoryRepository categories,
        IPurchaseConditionRepository conditions,
        ICreditCardPurchaseRepository purchases,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(purchases);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _categories = categories;
        _conditions = conditions;
        _purchases = purchases;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        UpdateCreditCardPurchaseDetailsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _accountAccess.GetRequiredUserId();

        var purchase = await _purchases.GetByIdAsync(command.PurchaseId, cancellationToken)
                       ?? throw new NotFoundException(nameof(CreditCardPurchase), command.PurchaseId);

        var account = await _accountAccess.EnsureCanAccessEntityAsync(
            purchase.AccountId, nameof(CreditCardPurchase), purchase.Id, cancellationToken);

        if (command.CategoryId != purchase.CategoryId)
        {
            await CategoryValidation.EnsureUsableForUserAsync(
                _categories, account.OwnerUserId, command.CategoryId, cancellationToken);
        }

        if (command.ConditionId != purchase.ConditionId)
        {
            await PurchaseConditionValidation.EnsureUsableForUserAsync(
                _conditions, account.OwnerUserId, command.ConditionId, cancellationToken);
        }

        purchase.UpdateDescription(command.Description);
        purchase.UpdateCategory(command.CategoryId);
        purchase.UpdateCondition(command.ConditionId);

        await _purchases.UpdateAsync(purchase, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
