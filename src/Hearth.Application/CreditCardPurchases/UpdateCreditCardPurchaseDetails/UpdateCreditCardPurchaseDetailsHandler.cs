using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Categories;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.CreditCardPurchases.UpdateCreditCardPurchaseDetails;

public sealed class UpdateCreditCardPurchaseDetailsHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly ICategoryRepository _categories;
    private readonly ICreditCardPurchaseRepository _purchases;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCreditCardPurchaseDetailsHandler(
        AccountAccessGuard accountAccess,
        ICategoryRepository categories,
        ICreditCardPurchaseRepository purchases,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(purchases);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _categories = categories;
        _purchases = purchases;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        UpdateCreditCardPurchaseDetailsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _accountAccess.GetRequiredUserId();
        var condition = PurchaseConditionMapper.ToDomain(command.ConditionKind, command.ConditionName);

        var purchase = await _purchases.GetByIdAsync(command.PurchaseId, cancellationToken)
                       ?? throw new NotFoundException(nameof(CreditCardPurchase), command.PurchaseId);

        await _accountAccess.EnsureCanAccessEntityAsync(
            purchase.AccountId, nameof(CreditCardPurchase), purchase.Id, cancellationToken);

        if (command.CategoryId != purchase.CategoryId)
        {
            await CategoryValidation.EnsureUsableForAccountAsync(
                _categories, purchase.AccountId, command.CategoryId, cancellationToken);
        }

        purchase.UpdateDescription(command.Description);
        purchase.UpdateCategory(command.CategoryId);
        purchase.UpdateCondition(condition);

        await _purchases.UpdateAsync(purchase, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
