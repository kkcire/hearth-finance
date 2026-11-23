using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.FixedExpenses.MarkFixedExpenseAsPaid;

public sealed class MarkFixedExpenseAsPaidHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IFixedExpenseRepository _fixedExpenses;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public MarkFixedExpenseAsPaidHandler(
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

    public async Task<bool> ExecuteAsync(
        MarkFixedExpenseAsPaidCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _accountAccess.GetRequiredUserId();
        var month = YearMonth.Of(command.Year, command.Month);

        var expense = await _fixedExpenses.GetByIdAsync(command.FixedExpenseId, cancellationToken)
                      ?? throw new NotFoundException(nameof(FixedExpense), command.FixedExpenseId);

        var account = await _accountAccess.EnsureCanAccessEntityAsync(
            expense.AccountId, nameof(FixedExpense), expense.Id, cancellationToken);

        account.EnsureCanRegisterOn(command.PaidOn);

        var changed = expense.MarkAsPaid(month, command.PaidOn, _clock.Today);

        if (changed)
        {
            await _fixedExpenses.UpdateAsync(expense, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }
}
