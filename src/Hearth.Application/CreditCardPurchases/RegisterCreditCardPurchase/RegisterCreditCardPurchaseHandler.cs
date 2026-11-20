using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.CreditCardPurchases.RegisterCreditCardPurchase;

public sealed class RegisterCreditCardPurchaseHandler
{
    private readonly IAccountRepository _accounts;
    private readonly ICreditCardPurchaseRepository _purchases;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterCreditCardPurchaseHandler(
        IAccountRepository accounts,
        ICreditCardPurchaseRepository purchases,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(purchases);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accounts = accounts;
        _purchases = purchases;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> ExecuteAsync(
        RegisterCreditCardPurchaseCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var amount = Money.FromDecimal(command.Amount);
        var condition = PurchaseConditionMapper.ToDomain(command.ConditionKind, command.ConditionName);

        var account = await _accounts.GetByIdAsync(command.AccountId, cancellationToken)
                      ?? throw new NotFoundException(nameof(Account), command.AccountId);

        var purchase = CreditCardPurchase.Create(
            Guid.CreateVersion7(),
            account.Id,
            command.Date,
            amount,
            command.InstallmentCount,
            command.InitialInstallment,
            account.BillingRule,
            condition,
            command.Description,
            command.Category);

        var dateToValidate = purchase.InitialInstallment == 1
            ? purchase.Date
            : purchase.FirstCycle.AddCycles(purchase.InitialInstallment - 1).ClosingDate;

        account.EnsureCanRegisterOn(dateToValidate);

        await _purchases.AddAsync(purchase, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return purchase.Id;
    }
}
