using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.DirectExpenses.DeleteDirectExpense;

public sealed class DeleteDirectExpenseHandler
{
    private readonly IDirectExpenseRepository _directExpenses;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteDirectExpenseHandler(
        IDirectExpenseRepository directExpenses,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(directExpenses);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _directExpenses = directExpenses;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        DeleteDirectExpenseCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var expense = await _directExpenses.GetByIdAsync(command.DirectExpenseId, cancellationToken)
                      ?? throw new NotFoundException(nameof(DirectExpense), command.DirectExpenseId);

        await _directExpenses.DeleteAsync(expense, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
