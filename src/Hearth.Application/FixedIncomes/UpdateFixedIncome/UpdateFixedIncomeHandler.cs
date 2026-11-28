using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Application.PurchaseConditions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.FixedIncomes.UpdateFixedIncome;

public sealed class UpdateFixedIncomeHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IFixedIncomeRepository _fixedIncomes;
    private readonly IPurchaseConditionRepository _conditions;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateFixedIncomeHandler(
        AccountAccessGuard accountAccess,
        IFixedIncomeRepository fixedIncomes,
        IPurchaseConditionRepository conditions,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(fixedIncomes);
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _fixedIncomes = fixedIncomes;
        _conditions = conditions;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        UpdateFixedIncomeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.EffectiveFromYear.HasValue != command.EffectiveFromMonth.HasValue)
        {
            throw new ArgumentException(
                "EffectiveFromYear and EffectiveFromMonth must either both be provided or both be null.",
                nameof(command));
        }

        _accountAccess.GetRequiredUserId();

        var income = await _fixedIncomes.GetByIdAsync(command.FixedIncomeId, cancellationToken)
                     ?? throw new NotFoundException(nameof(FixedIncome), command.FixedIncomeId);

        var account = await _accountAccess.EnsureCanAccessEntityAsync(
            income.AccountId, nameof(FixedIncome), income.Id, cancellationToken);

        var targetConditionId = command.ConditionId ?? income.ConditionId;
        if (targetConditionId != income.ConditionId)
        {
            await PurchaseConditionValidation.EnsureUsableForUserAsync(
                _conditions, account.OwnerUserId, targetConditionId, cancellationToken);
        }

        income.UpdateDetails(command.Description, command.ExpectedDayOfMonth, targetConditionId);

        if (command.NewAmount is { } rawAmount)
        {
            var effectiveFrom = command.EffectiveFromYear.HasValue
                ? YearMonth.Of(command.EffectiveFromYear.Value, command.EffectiveFromMonth!.Value)
                : YearMonth.From(_clock.Today);

            income.ChangeAmountFrom(effectiveFrom, Money.FromDecimal(rawAmount));
        }

        await _fixedIncomes.UpdateAsync(income, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
