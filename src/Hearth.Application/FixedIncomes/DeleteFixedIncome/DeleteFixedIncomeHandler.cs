using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.FixedIncomes.DeleteFixedIncome;

public sealed class DeleteFixedIncomeHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IFixedIncomeRepository _fixedIncomes;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteFixedIncomeHandler(
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
        DeleteFixedIncomeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _accountAccess.GetRequiredUserId();

        var income = await _fixedIncomes.GetByIdAsync(command.FixedIncomeId, cancellationToken)
                     ?? throw new NotFoundException(nameof(FixedIncome), command.FixedIncomeId);

        _ = await _accountAccess.EnsureCanAccessEntityAsync(
            income.AccountId, nameof(FixedIncome), income.Id, cancellationToken);

        await _fixedIncomes.DeleteAsync(income, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
