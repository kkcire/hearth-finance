using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.CategoryBudgets.EndCategoryBudget;

public sealed class EndCategoryBudgetHandler
{
    private readonly ICategoryBudgetRepository _budgets;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public EndCategoryBudgetHandler(
        ICategoryBudgetRepository budgets,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(budgets);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _budgets = budgets;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        EndCategoryBudgetCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var lastMonth = YearMonth.Of(command.LastYear, command.LastMonth);

        var budget = await _budgets.GetByIdAsync(command.CategoryBudgetId, cancellationToken)
                     ?? throw new NotFoundException(nameof(CategoryBudget), command.CategoryBudgetId);

        if (lastMonth >= budget.StartMonth && budget.EndMonth is { } currentEnd && lastMonth > currentEnd)
        {
            var siblings = await _budgets.GetByAccountAndCategoryAsync(
                budget.AccountId, budget.CategoryId, cancellationToken);

            if (siblings.Any(other => other.Id != budget.Id && other.OverlapsWith(budget.StartMonth, lastMonth)))
            {
                throw new DomainException(
                    "Extending this budget would overlap another budget for the same category.");
            }
        }

        budget.End(lastMonth, _clock.Today);

        await _budgets.UpdateAsync(budget, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
