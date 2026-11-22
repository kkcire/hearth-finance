using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.Incomes.MarkIncomeAsReceived;

public sealed class MarkIncomeAsReceivedHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IIncomeRepository _incomes;
    private readonly IUnitOfWork _unitOfWork;

    public MarkIncomeAsReceivedHandler(
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

    public async Task<bool> ExecuteAsync(
        MarkIncomeAsReceivedCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _accountAccess.GetRequiredUserId();

        var income = await _incomes.GetByIdAsync(command.IncomeId, cancellationToken)
                     ?? throw new NotFoundException(nameof(Income), command.IncomeId);

        var account = await _accountAccess.EnsureCanAccessEntityAsync(
            income.AccountId, nameof(Income), income.Id, cancellationToken);

        account.EnsureCanRegisterOn(command.ReceivedOn);

        var changed = income.MarkAsReceived(command.ReceivedOn);

        if (changed)
        {
            await _incomes.UpdateAsync(income, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }
}
