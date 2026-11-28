using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.FixedIncomes.ConfirmFixedIncomeReceipt;

public sealed class ConfirmFixedIncomeReceiptHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IFixedIncomeRepository _fixedIncomes;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmFixedIncomeReceiptHandler(
        AccountAccessGuard accountAccess,
        IFixedIncomeRepository fixedIncomes,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(fixedIncomes);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _fixedIncomes = fixedIncomes;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<FixedIncomeReceiptDto> ExecuteAsync(
        ConfirmFixedIncomeReceiptCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _accountAccess.GetRequiredUserId();
        var month = YearMonth.Of(command.Year, command.Month);
        var actualAmount = Money.FromDecimal(command.ActualAmount);

        var income = await _fixedIncomes.GetByIdAsync(command.FixedIncomeId, cancellationToken)
                     ?? throw new NotFoundException(nameof(FixedIncome), command.FixedIncomeId);

        var account = await _accountAccess.EnsureCanAccessEntityAsync(
            income.AccountId, nameof(FixedIncome), income.Id, cancellationToken);

        var receipt = income.ConfirmReceipt(account, month, command.ReceivedOn, actualAmount, _clock.Today);

        await _fixedIncomes.UpdateAsync(income, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new FixedIncomeReceiptDto(
            receipt.Month.Year,
            receipt.Month.Month,
            receipt.ReceivedOn,
            receipt.ActualAmount.Amount);
    }
}
