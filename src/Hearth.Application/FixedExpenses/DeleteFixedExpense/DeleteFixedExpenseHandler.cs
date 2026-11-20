using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.FixedExpenses.DeleteFixedExpense;

public sealed class DeleteFixedExpenseHandler
{
    private readonly IFixedExpenseRepository _fixedExpenses;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteFixedExpenseHandler(
        IFixedExpenseRepository fixedExpenses,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _fixedExpenses = fixedExpenses;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        DeleteFixedExpenseCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var expense = await _fixedExpenses.GetByIdAsync(command.FixedExpenseId, cancellationToken)
                      ?? throw new NotFoundException(nameof(FixedExpense), command.FixedExpenseId);

        await _fixedExpenses.DeleteAsync(expense, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
