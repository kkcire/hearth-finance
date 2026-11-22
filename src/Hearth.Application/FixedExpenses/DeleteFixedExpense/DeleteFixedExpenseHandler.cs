using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.FixedExpenses.DeleteFixedExpense;

public sealed class DeleteFixedExpenseHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IFixedExpenseRepository _fixedExpenses;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteFixedExpenseHandler(
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

    public async Task ExecuteAsync(
        DeleteFixedExpenseCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _accountAccess.GetRequiredUserId();

        var expense = await _fixedExpenses.GetByIdAsync(command.FixedExpenseId, cancellationToken)
                      ?? throw new NotFoundException(nameof(FixedExpense), command.FixedExpenseId);

        await _accountAccess.EnsureCanAccessEntityAsync(
            expense.AccountId, nameof(FixedExpense), expense.Id, cancellationToken);

        await _fixedExpenses.DeleteAsync(expense, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
