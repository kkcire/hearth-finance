using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.CategoryBudgets.DeleteCategoryBudget;

public sealed class DeleteCategoryBudgetHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly ICategoryBudgetRepository _budgets;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCategoryBudgetHandler(
        AccountAccessGuard accountAccess,
        ICategoryBudgetRepository budgets,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(budgets);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _budgets = budgets;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        DeleteCategoryBudgetCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _accountAccess.GetRequiredUserId();

        var budget = await _budgets.GetByIdAsync(command.CategoryBudgetId, cancellationToken)
                     ?? throw new NotFoundException(nameof(CategoryBudget), command.CategoryBudgetId);

        await _accountAccess.EnsureCanAccessEntityAsync(
            budget.AccountId, nameof(CategoryBudget), budget.Id, cancellationToken);

        await _budgets.DeleteAsync(budget, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
