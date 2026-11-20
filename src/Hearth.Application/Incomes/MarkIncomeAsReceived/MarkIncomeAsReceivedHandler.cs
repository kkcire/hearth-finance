using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.Incomes.MarkIncomeAsReceived;

public sealed class MarkIncomeAsReceivedHandler
{
    private readonly IAccountRepository _accounts;
    private readonly IIncomeRepository _incomes;
    private readonly IUnitOfWork _unitOfWork;

    public MarkIncomeAsReceivedHandler(
        IAccountRepository accounts,
        IIncomeRepository incomes,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(incomes);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accounts = accounts;
        _incomes = incomes;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> ExecuteAsync(
        MarkIncomeAsReceivedCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var income = await _incomes.GetByIdAsync(command.IncomeId, cancellationToken)
                     ?? throw new NotFoundException(nameof(Income), command.IncomeId);

        var account = await _accounts.GetByIdAsync(income.AccountId, cancellationToken)
                      ?? throw new NotFoundException(nameof(Account), income.AccountId);

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
