using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.CreditCardPurchases;
using Hearth.Domain.Services;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Reports.GetExpensesByCategory;

public sealed class GetExpensesByCategoryHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly ICategoryRepository _categories;
    private readonly IDirectExpenseRepository _directExpenses;
    private readonly IFixedExpenseRepository _fixedExpenses;
    private readonly ICreditCardPurchaseRepository _purchases;

    public GetExpensesByCategoryHandler(
        AccountAccessGuard accountAccess,
        ICategoryRepository categories,
        IDirectExpenseRepository directExpenses,
        IFixedExpenseRepository fixedExpenses,
        ICreditCardPurchaseRepository purchases)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(directExpenses);
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(purchases);

        _accountAccess = accountAccess;
        _categories = categories;
        _directExpenses = directExpenses;
        _fixedExpenses = fixedExpenses;
        _purchases = purchases;
    }

    public async Task<CategorySpendingDto> ExecuteAsync(
        GetExpensesByCategoryQuery query,
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

        var account = await _accountAccess.GetAuthorizedAccountAsync(query.AccountId, cancellationToken);

        var categories = await _categories.GetByAccountAsync(account.Id, cancellationToken);
        var categoryNames = categories.ToDictionary(category => category.Id, category => category.Name);

        var directExpenses = await _directExpenses.GetByAccountAsync(account.Id, cancellationToken);
        var fixedExpenses = await _fixedExpenses.GetByAccountAsync(account.Id, cancellationToken);
        var purchases = await _purchases.GetByAccountAsync(account.Id, cancellationToken);

        var spending = CategorySpendingCalculator.Calculate(
            account.Id,
            month,
            directExpenses,
            fixedExpenses,
            purchases,
            query.IncludeDirectExpenses,
            query.IncludeFixedExpenses,
            query.IncludeCardInstallments,
            includedConditions);

        return CategorySpendingDto.FromDomain(account.Id, spending, categoryNames);
    }
}
