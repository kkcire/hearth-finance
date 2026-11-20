using Hearth.Application.Abstractions;
using Hearth.Application.Categories;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.FixedExpenses.UpdateFixedExpenseDetails;

public sealed class UpdateFixedExpenseDetailsHandler
{
    private readonly ICategoryRepository _categories;
    private readonly IFixedExpenseRepository _fixedExpenses;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateFixedExpenseDetailsHandler(
        ICategoryRepository categories,
        IFixedExpenseRepository fixedExpenses,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _categories = categories;
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

        if (command.CategoryId != expense.CategoryId)
        {
            await CategoryValidation.EnsureUsableForAccountAsync(
                _categories, expense.AccountId, command.CategoryId, cancellationToken);
        }

        expense.UpdateDescription(command.Description);
        expense.UpdateCategory(command.CategoryId);

        await _fixedExpenses.UpdateAsync(expense, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
