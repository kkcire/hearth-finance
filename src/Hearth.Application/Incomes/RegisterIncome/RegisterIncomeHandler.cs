using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.PurchaseConditions;
using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Incomes.RegisterIncome;

public sealed class RegisterIncomeHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IIncomeRepository _incomes;
    private readonly IPurchaseConditionRepository _conditions;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterIncomeHandler(
        AccountAccessGuard accountAccess,
        IIncomeRepository incomes,
        IPurchaseConditionRepository conditions,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(incomes);
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _incomes = incomes;
        _conditions = conditions;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> ExecuteAsync(
        RegisterIncomeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var amount = Money.FromDecimal(command.Amount);
        var conditionId = command.ConditionId ?? SystemPurchaseConditions.OwnId;

        var account = await _accountAccess.GetAuthorizedAccountAsync(command.AccountId, cancellationToken);

        await PurchaseConditionValidation.EnsureUsableForUserAsync(
            _conditions, account.OwnerUserId, conditionId, cancellationToken);

        var income = Income.Create(
            Guid.CreateVersion7(),
            account,
            command.ExpectedDate,
            amount,
            command.Description,
            conditionId);

        if (command.ReceivedOn is { } received)
        {
            income.MarkAsReceived(account, received, _clock.Today);
        }

        await _incomes.AddAsync(income, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return income.Id;
    }
}
