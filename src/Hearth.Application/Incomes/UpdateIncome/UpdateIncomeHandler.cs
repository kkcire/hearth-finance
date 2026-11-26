using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Application.PurchaseConditions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Incomes.UpdateIncome;

public sealed class UpdateIncomeHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IIncomeRepository _incomes;
    private readonly IPurchaseConditionRepository _conditions;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateIncomeHandler(
        AccountAccessGuard accountAccess,
        IIncomeRepository incomes,
        IPurchaseConditionRepository conditions,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(incomes);
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _incomes = incomes;
        _conditions = conditions;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        UpdateIncomeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _accountAccess.GetRequiredUserId();
        var amount = Money.FromDecimal(command.Amount);

        var income = await _incomes.GetByIdAsync(command.IncomeId, cancellationToken)
                     ?? throw new NotFoundException(nameof(Income), command.IncomeId);

        var account = await _accountAccess.EnsureCanAccessEntityAsync(
            income.AccountId, nameof(Income), income.Id, cancellationToken);

        account.EnsureCanRegisterOn(command.ExpectedDate);

        if (command.ConditionId is { } newConditionId && newConditionId != income.ConditionId)
        {
            await PurchaseConditionValidation.EnsureUsableForUserAsync(
                _conditions, account.OwnerUserId, newConditionId, cancellationToken);
            income.UpdateCondition(newConditionId);
        }

        if (command.ExpectedDate != income.ExpectedDate)
        {
            income.UpdateExpectedDate(account, command.ExpectedDate);
        }

        if (amount != income.Amount)
        {
            income.UpdateAmount(amount);
        }

        income.UpdateDescription(command.Description);

        await _incomes.UpdateAsync(income, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
