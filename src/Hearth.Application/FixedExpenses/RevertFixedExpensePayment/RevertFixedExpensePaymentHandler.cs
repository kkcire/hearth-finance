using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.FixedExpenses.RevertFixedExpensePayment;

public sealed class RevertFixedExpensePaymentHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IFixedExpenseRepository _fixedExpenses;
    private readonly IUnitOfWork _unitOfWork;

    public RevertFixedExpensePaymentHandler(
        AccountAccessGuard accountAccess,
        IFixedExpenseRepository fixedExpenses,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _fixedExpenses = fixedExpenses;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> ExecuteAsync(
        RevertFixedExpensePaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _accountAccess.GetRequiredUserId();
        var month = YearMonth.Of(command.Year, command.Month);

        var expense = await _fixedExpenses.GetByIdAsync(command.FixedExpenseId, cancellationToken)
                      ?? throw new NotFoundException(nameof(FixedExpense), command.FixedExpenseId);

        await _accountAccess.EnsureCanAccessEntityAsync(
            expense.AccountId, nameof(FixedExpense), expense.Id, cancellationToken);

        var changed = expense.RevertPayment(month);

        if (changed)
        {
            await _fixedExpenses.UpdateAsync(expense, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }
}
