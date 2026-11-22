using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.CategoryBudgets.ChangeCategoryBudgetAmount;

public sealed class ChangeCategoryBudgetAmountHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly ICategoryBudgetRepository _budgets;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ChangeCategoryBudgetAmountHandler(
        AccountAccessGuard accountAccess,
        ICategoryBudgetRepository budgets,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(budgets);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _budgets = budgets;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<ChangeCategoryBudgetAmountResult> ExecuteAsync(
        ChangeCategoryBudgetAmountCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _accountAccess.GetRequiredUserId();
        var newAmount = Money.FromDecimal(command.NewAmount);

        var budget = await _budgets.GetByIdAsync(command.CategoryBudgetId, cancellationToken)
                     ?? throw new NotFoundException(nameof(CategoryBudget), command.CategoryBudgetId);

        await _accountAccess.EnsureCanAccessEntityAsync(
            budget.AccountId, nameof(CategoryBudget), budget.Id, cancellationToken);

        var effectiveFrom = budget.ChangeAmount(newAmount, _clock.Today);

        await _budgets.UpdateAsync(budget, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ChangeCategoryBudgetAmountResult(effectiveFrom.Year, effectiveFrom.Month);
    }
}
