using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.FixedIncomes.RevertFixedIncomeReceipt;

public sealed class RevertFixedIncomeReceiptHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IFixedIncomeRepository _fixedIncomes;
    private readonly IUnitOfWork _unitOfWork;

    public RevertFixedIncomeReceiptHandler(
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

    public async Task<bool> ExecuteAsync(
        RevertFixedIncomeReceiptCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _accountAccess.GetRequiredUserId();
        var month = YearMonth.Of(command.Year, command.Month);

        var income = await _fixedIncomes.GetByIdAsync(command.FixedIncomeId, cancellationToken)
                     ?? throw new NotFoundException(nameof(FixedIncome), command.FixedIncomeId);

        _ = await _accountAccess.EnsureCanAccessEntityAsync(
            income.AccountId, nameof(FixedIncome), income.Id, cancellationToken);

        var changed = income.RevertReceipt(month);
        if (changed)
        {
            await _fixedIncomes.UpdateAsync(income, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }
}
