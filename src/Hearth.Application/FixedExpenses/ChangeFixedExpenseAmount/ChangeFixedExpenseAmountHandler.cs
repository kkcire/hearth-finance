using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.FixedExpenses.ChangeFixedExpenseAmount;

public sealed class ChangeFixedExpenseAmountHandler
{
    private readonly IAccountRepository _accounts;
    private readonly IFixedExpenseRepository _fixedExpenses;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ChangeFixedExpenseAmountHandler(
        IAccountRepository accounts,
        IFixedExpenseRepository fixedExpenses,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accounts = accounts;
        _fixedExpenses = fixedExpenses;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<ChangeFixedExpenseAmountResult> ExecuteAsync(
        ChangeFixedExpenseAmountCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var newAmount = Money.FromDecimal(command.NewAmount);

        var expense = await _fixedExpenses.GetByIdAsync(command.FixedExpenseId, cancellationToken)
                      ?? throw new NotFoundException(nameof(FixedExpense), command.FixedExpenseId);

        var account = await _accounts.GetByIdAsync(expense.AccountId, cancellationToken)
                      ?? throw new NotFoundException(nameof(Account), expense.AccountId);

        var effectiveFrom = expense.ChangeAmount(newAmount, _clock.Today, account.BillingRule);

        await _fixedExpenses.UpdateAsync(expense, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ChangeFixedExpenseAmountResult(effectiveFrom.Year, effectiveFrom.Month);
    }
}
