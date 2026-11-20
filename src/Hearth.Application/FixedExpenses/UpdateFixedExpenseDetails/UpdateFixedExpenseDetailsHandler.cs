using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.FixedExpenses.UpdateFixedExpenseDetails;

public sealed class UpdateFixedExpenseDetailsHandler
{
    private readonly IFixedExpenseRepository _fixedExpenses;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateFixedExpenseDetailsHandler(
        IFixedExpenseRepository fixedExpenses,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _fixedExpenses = fixedExpenses;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        UpdateFixedExpenseDetailsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var expense = await _fixedExpenses.GetByIdAsync(command.FixedExpenseId, cancellationToken)
                      ?? throw new NotFoundException(nameof(FixedExpense), command.FixedExpenseId);

        expense.UpdateDescription(command.Description);
        expense.UpdateCategory(command.Category);

        await _fixedExpenses.UpdateAsync(expense, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
