using Hearth.Application.Abstractions;
using Hearth.Application.CreditCardPurchases;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.Services;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Reports.GetBudgetStatus;

public sealed class GetBudgetStatusHandler
{
    private readonly IAccountRepository _accounts;
    private readonly ICategoryRepository _categories;
    private readonly ICategoryBudgetRepository _budgets;
    private readonly IDirectExpenseRepository _directExpenses;
    private readonly IFixedExpenseRepository _fixedExpenses;
    private readonly ICreditCardPurchaseRepository _purchases;

    public GetBudgetStatusHandler(
        IAccountRepository accounts,
        ICategoryRepository categories,
        ICategoryBudgetRepository budgets,
        IDirectExpenseRepository directExpenses,
        IFixedExpenseRepository fixedExpenses,
        ICreditCardPurchaseRepository purchases)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(budgets);
        ArgumentNullException.ThrowIfNull(directExpenses);
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(purchases);

        _accounts = accounts;
        _categories = categories;
        _budgets = budgets;
        _directExpenses = directExpenses;
        _fixedExpenses = fixedExpenses;
        _purchases = purchases;
    }

    public async Task<BudgetStatusDto> ExecuteAsync(
        GetBudgetStatusQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var month = YearMonth.Of(query.Year, query.Month);

        IReadOnlyCollection<PurchaseCondition>? includedConditions = null;
        if (query.IncludedConditions is not null)
        {
            var converted = new List<PurchaseCondition>(query.IncludedConditions.Count);
            foreach (var filter in query.IncludedConditions)
            {
                if (filter is null)
                {
                    throw new ArgumentException(
                        "The included conditions must not contain null entries.",
                        nameof(query));
                }

                converted.Add(PurchaseConditionMapper.ToDomain(filter.Kind, filter.Name));
            }

            includedConditions = converted;
        }

        var account = await _accounts.GetByIdAsync(query.AccountId, cancellationToken)
                      ?? throw new NotFoundException(nameof(Account), query.AccountId);

        var categories = await _categories.GetByAccountAsync(account.Id, cancellationToken);
        var categoryNames = categories.ToDictionary(category => category.Id, category => category.Name);

        var budgets = await _budgets.GetByAccountAsync(account.Id, cancellationToken);
        var directExpenses = await _directExpenses.GetByAccountAsync(account.Id, cancellationToken);
        var fixedExpenses = await _fixedExpenses.GetByAccountAsync(account.Id, cancellationToken);
        var purchases = await _purchases.GetByAccountAsync(account.Id, cancellationToken);

        var status = BudgetStatusCalculator.Calculate(
            account.Id,
            month,
            budgets,
            directExpenses,
            fixedExpenses,
            purchases,
            query.IncludeDirectExpenses,
            query.IncludeFixedExpenses,
            query.IncludeCardInstallments,
            includedConditions);

        return BudgetStatusDto.FromDomain(account.Id, status, categoryNames);
    }
}
