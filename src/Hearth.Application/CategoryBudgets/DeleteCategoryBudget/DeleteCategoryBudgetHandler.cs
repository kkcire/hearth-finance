using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.CategoryBudgets.DeleteCategoryBudget;

public sealed class DeleteCategoryBudgetHandler
{
    private readonly ICategoryBudgetRepository _budgets;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCategoryBudgetHandler(
        ICategoryBudgetRepository budgets,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(budgets);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _budgets = budgets;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        DeleteCategoryBudgetCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var budget = await _budgets.GetByIdAsync(command.CategoryBudgetId, cancellationToken)
                     ?? throw new NotFoundException(nameof(CategoryBudget), command.CategoryBudgetId);

        await _budgets.DeleteAsync(budget, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
