using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.FixedExpenses.RevertFixedExpensePayment;

public sealed class RevertFixedExpensePaymentHandler
{
    private readonly IFixedExpenseRepository _fixedExpenses;
    private readonly IUnitOfWork _unitOfWork;

    public RevertFixedExpensePaymentHandler(
        IFixedExpenseRepository fixedExpenses,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _fixedExpenses = fixedExpenses;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> ExecuteAsync(
        RevertFixedExpensePaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var month = YearMonth.Of(command.Year, command.Month);

        var expense = await _fixedExpenses.GetByIdAsync(command.FixedExpenseId, cancellationToken)
                      ?? throw new NotFoundException(nameof(FixedExpense), command.FixedExpenseId);

        var changed = expense.RevertPayment(month);

        if (changed)
        {
            await _fixedExpenses.UpdateAsync(expense, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }
}
