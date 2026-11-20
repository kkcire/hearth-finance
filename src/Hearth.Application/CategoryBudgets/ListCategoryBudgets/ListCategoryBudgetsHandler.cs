using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.CategoryBudgets.ListCategoryBudgets;

public sealed class ListCategoryBudgetsHandler
{
    private readonly IAccountRepository _accounts;
    private readonly ICategoryRepository _categories;
    private readonly ICategoryBudgetRepository _budgets;

    public ListCategoryBudgetsHandler(
        IAccountRepository accounts,
        ICategoryRepository categories,
        ICategoryBudgetRepository budgets)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(budgets);

        _accounts = accounts;
        _categories = categories;
        _budgets = budgets;
    }

    public async Task<IReadOnlyList<CategoryBudgetDto>> ExecuteAsync(
        ListCategoryBudgetsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        _ = await _accounts.GetByIdAsync(query.AccountId, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), query.AccountId);

        var categories = await _categories.GetByAccountAsync(query.AccountId, cancellationToken);
        var categoryNames = categories.ToDictionary(category => category.Id, category => category.Name);

        var budgets = await _budgets.GetByAccountAsync(query.AccountId, cancellationToken);

        return budgets
            .Select(budget => CategoryBudgetDto.FromEntity(
                budget,
                categoryNames.GetValueOrDefault(budget.CategoryId, string.Empty)))
            .OrderByDescending(dto => dto.StartYear)
            .ThenByDescending(dto => dto.StartMonth)
            .ThenBy(dto => dto.CategoryName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
