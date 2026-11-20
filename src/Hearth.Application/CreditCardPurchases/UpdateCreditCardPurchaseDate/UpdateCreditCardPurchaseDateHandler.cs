using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.CreditCardPurchases.UpdateCreditCardPurchaseDate;

public sealed class UpdateCreditCardPurchaseDateHandler
{
    private readonly IAccountRepository _accounts;
    private readonly ICreditCardPurchaseRepository _purchases;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCreditCardPurchaseDateHandler(
        IAccountRepository accounts,
        ICreditCardPurchaseRepository purchases,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(purchases);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accounts = accounts;
        _purchases = purchases;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        UpdateCreditCardPurchaseDateCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var purchase = await _purchases.GetByIdAsync(command.PurchaseId, cancellationToken)
                       ?? throw new NotFoundException(nameof(CreditCardPurchase), command.PurchaseId);

        var account = await _accounts.GetByIdAsync(purchase.AccountId, cancellationToken)
                      ?? throw new NotFoundException(nameof(Account), purchase.AccountId);

        if (!purchase.IsLockedAt(_clock.Today))
        {
            var dateToValidate = purchase.InitialInstallment == 1
                ? command.NewDate
                : BillingCycle.ForPurchase(command.NewDate, purchase.Rule)
                    .AddCycles(purchase.InitialInstallment - 1)
                    .ClosingDate;

            account.EnsureCanRegisterOn(dateToValidate);
        }

        purchase.UpdateDate(command.NewDate, _clock.Today);

        await _purchases.UpdateAsync(purchase, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
