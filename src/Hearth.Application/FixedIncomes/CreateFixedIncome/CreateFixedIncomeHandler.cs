using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.PurchaseConditions;
using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.FixedIncomes.CreateFixedIncome;

public sealed class CreateFixedIncomeHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IFixedIncomeRepository _fixedIncomes;
    private readonly IPurchaseConditionRepository _conditions;
    private readonly IUnitOfWork _unitOfWork;

    public CreateFixedIncomeHandler(
        AccountAccessGuard accountAccess,
        IFixedIncomeRepository fixedIncomes,
        IPurchaseConditionRepository conditions,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(fixedIncomes);
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _fixedIncomes = fixedIncomes;
        _conditions = conditions;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> ExecuteAsync(
        CreateFixedIncomeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.EndYear.HasValue != command.EndMonth.HasValue)
        {
            throw new ArgumentException("EndYear and EndMonth must either both be provided or both be null.", nameof(command));
        }

        var startMonth = YearMonth.Of(command.StartYear, command.StartMonth);
        var endMonth = command.EndYear.HasValue
            ? YearMonth.Of(command.EndYear.Value, command.EndMonth!.Value)
            : null;
        var amount = Money.FromDecimal(command.Amount);
        var conditionId = command.ConditionId ?? SystemPurchaseConditions.OwnId;

        var account = await _accountAccess.GetAuthorizedAccountAsync(command.AccountId, cancellationToken);

        await PurchaseConditionValidation.EnsureUsableForUserAsync(
            _conditions, account.OwnerUserId, conditionId, cancellationToken);

        var income = FixedIncome.Create(
            Guid.CreateVersion7(),
            account,
            startMonth,
            endMonth,
            command.ExpectedDayOfMonth,
            amount,
            command.Description,
            conditionId);

        await _fixedIncomes.AddAsync(income, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return income.Id;
    }
}
