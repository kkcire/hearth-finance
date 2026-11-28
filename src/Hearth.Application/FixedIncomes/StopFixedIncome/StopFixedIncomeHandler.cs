using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.FixedIncomes.StopFixedIncome;

public sealed class StopFixedIncomeHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IFixedIncomeRepository _fixedIncomes;
    private readonly IUnitOfWork _unitOfWork;

    public StopFixedIncomeHandler(
        AccountAccessGuard accountAccess,
        IFixedIncomeRepository fixedIncomes,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(fixedIncomes);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _fixedIncomes = fixedIncomes;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        StopFixedIncomeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.EndYear.HasValue != command.EndMonth.HasValue)
        {
            throw new ArgumentException("EndYear and EndMonth must either both be provided or both be null.", nameof(command));
        }

        _accountAccess.GetRequiredUserId();

        var income = await _fixedIncomes.GetByIdAsync(command.FixedIncomeId, cancellationToken)
                     ?? throw new NotFoundException(nameof(FixedIncome), command.FixedIncomeId);

        _ = await _accountAccess.EnsureCanAccessEntityAsync(
            income.AccountId, nameof(FixedIncome), income.Id, cancellationToken);

        if (command.EndYear.HasValue)
        {
            income.StopAt(YearMonth.Of(command.EndYear.Value, command.EndMonth!.Value));
        }
        else
        {
            income.Reactivate();
        }

        await _fixedIncomes.UpdateAsync(income, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
