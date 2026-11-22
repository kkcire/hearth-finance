using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.FixedExpenses.ChangeFixedExpenseAmount;

public sealed class ChangeFixedExpenseAmountHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IFixedExpenseRepository _fixedExpenses;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ChangeFixedExpenseAmountHandler(
        AccountAccessGuard accountAccess,
        IFixedExpenseRepository fixedExpenses,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _fixedExpenses = fixedExpenses;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<ChangeFixedExpenseAmountResult> ExecuteAsync(
        ChangeFixedExpenseAmountCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _accountAccess.GetRequiredUserId();
        var newAmount = Money.FromDecimal(command.NewAmount);

        var expense = await _fixedExpenses.GetByIdAsync(command.FixedExpenseId, cancellationToken)
                      ?? throw new NotFoundException(nameof(FixedExpense), command.FixedExpenseId);

        var account = await _accountAccess.EnsureCanAccessEntityAsync(
            expense.AccountId, nameof(FixedExpense), expense.Id, cancellationToken);

        var effectiveFrom = expense.ChangeAmount(newAmount, _clock.Today, account.BillingRule);

        await _fixedExpenses.UpdateAsync(expense, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ChangeFixedExpenseAmountResult(effectiveFrom.Year, effectiveFrom.Month);
    }
}
