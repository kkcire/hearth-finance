using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.FixedExpenses.MarkFixedExpenseAsPaid;

public sealed class MarkFixedExpenseAsPaidHandler
{
    private readonly IAccountRepository _accounts;
    private readonly IFixedExpenseRepository _fixedExpenses;
    private readonly IUnitOfWork _unitOfWork;

    public MarkFixedExpenseAsPaidHandler(
        IAccountRepository accounts,
        IFixedExpenseRepository fixedExpenses,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accounts = accounts;
        _fixedExpenses = fixedExpenses;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> ExecuteAsync(
        MarkFixedExpenseAsPaidCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var month = YearMonth.Of(command.Year, command.Month);

        var expense = await _fixedExpenses.GetByIdAsync(command.FixedExpenseId, cancellationToken)
                      ?? throw new NotFoundException(nameof(FixedExpense), command.FixedExpenseId);

        var account = await _accounts.GetByIdAsync(expense.AccountId, cancellationToken)
                      ?? throw new NotFoundException(nameof(Account), expense.AccountId);

        account.EnsureCanRegisterOn(command.PaidOn);

        var changed = expense.MarkAsPaid(month, command.PaidOn);

        if (changed)
        {
            await _fixedExpenses.UpdateAsync(expense, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }
}
