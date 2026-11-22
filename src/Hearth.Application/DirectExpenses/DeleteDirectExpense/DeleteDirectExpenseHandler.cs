using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.DirectExpenses.DeleteDirectExpense;

public sealed class DeleteDirectExpenseHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IDirectExpenseRepository _directExpenses;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteDirectExpenseHandler(
        AccountAccessGuard accountAccess,
        IDirectExpenseRepository directExpenses,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(directExpenses);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _directExpenses = directExpenses;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        DeleteDirectExpenseCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _accountAccess.GetRequiredUserId();

        var expense = await _directExpenses.GetByIdAsync(command.DirectExpenseId, cancellationToken)
                      ?? throw new NotFoundException(nameof(DirectExpense), command.DirectExpenseId);

        await _accountAccess.EnsureCanAccessEntityAsync(
            expense.AccountId, nameof(DirectExpense), expense.Id, cancellationToken);

        await _directExpenses.DeleteAsync(expense, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
