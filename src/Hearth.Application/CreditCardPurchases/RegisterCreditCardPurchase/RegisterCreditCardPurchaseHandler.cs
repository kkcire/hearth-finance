using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Categories;
using Hearth.Application.Exceptions;
using Hearth.Application.PurchaseConditions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.CreditCardPurchases.RegisterCreditCardPurchase;

public sealed class RegisterCreditCardPurchaseHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly ICreditCardRepository _creditCards;
    private readonly ICategoryRepository _categories;
    private readonly IPurchaseConditionRepository _conditions;
    private readonly ICreditCardPurchaseRepository _purchases;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterCreditCardPurchaseHandler(
        AccountAccessGuard accountAccess,
        ICreditCardRepository creditCards,
        ICategoryRepository categories,
        IPurchaseConditionRepository conditions,
        ICreditCardPurchaseRepository purchases,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(creditCards);
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(purchases);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _creditCards = creditCards;
        _categories = categories;
        _conditions = conditions;
        _purchases = purchases;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> ExecuteAsync(
        RegisterCreditCardPurchaseCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var amount = Money.FromDecimal(command.Amount);

        var account = await _accountAccess.GetAuthorizedAccountAsync(command.AccountId, cancellationToken);

        var card = await _creditCards.GetByIdAsync(command.CreditCardId, cancellationToken);
        if (card is null || card.AccountId != account.Id)
        {
            throw new NotFoundException(nameof(CreditCard), command.CreditCardId);
        }

        card.EnsureActive();

        await CategoryValidation.EnsureUsableForUserAsync(
            _categories, account.OwnerUserId, command.CategoryId, cancellationToken);

        await PurchaseConditionValidation.EnsureUsableForUserAsync(
            _conditions, account.OwnerUserId, command.ConditionId, cancellationToken);

        var purchase = CreditCardPurchase.Create(
            Guid.CreateVersion7(),
            account.Id,
            card.Id,
            command.Date,
            amount,
            command.InstallmentCount,
            command.InitialInstallment,
            card.BillingRule,
            command.ConditionId,
            command.Description,
            command.CategoryId);

        await _purchases.AddAsync(purchase, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return purchase.Id;
    }
}
