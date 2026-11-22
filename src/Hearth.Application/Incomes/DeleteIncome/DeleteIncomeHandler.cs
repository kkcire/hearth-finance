using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.Incomes.DeleteIncome;

public sealed class DeleteIncomeHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IIncomeRepository _incomes;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteIncomeHandler(
        AccountAccessGuard accountAccess,
        IIncomeRepository incomes,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(incomes);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _incomes = incomes;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        DeleteIncomeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _accountAccess.GetRequiredUserId();

        var income = await _incomes.GetByIdAsync(command.IncomeId, cancellationToken)
                     ?? throw new NotFoundException(nameof(Income), command.IncomeId);

        await _accountAccess.EnsureCanAccessEntityAsync(
            income.AccountId, nameof(Income), income.Id, cancellationToken);

        await _incomes.DeleteAsync(income, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
