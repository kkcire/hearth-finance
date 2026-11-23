using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Services;

/// <summary>
/// Compares active monthly category budgets of an account against accrual spending calculated
/// by <see cref="CategorySpendingCalculator"/>. Purely informational.
/// </summary>
public static class BudgetStatusCalculator
{
    public static BudgetStatus Calculate(
        Guid accountId,
        YearMonth month,
        IEnumerable<CategoryBudget> budgets,
        IEnumerable<DirectExpense> directExpenses,
        IEnumerable<FixedExpense> fixedExpenses,
        IEnumerable<CreditCardPurchase> purchases,
        bool includeDirectExpenses = true,
        bool includeFixedExpenses = true,
        bool includeCardInstallments = true,
        IReadOnlyCollection<PurchaseCondition>? includedConditions = null)
    {
        var filter = SpendingFilter.Create(
            includeDirectExpenses,
            includeFixedExpenses,
            includeCardInstallments,
            includedConditions);

        return Calculate(accountId, month, budgets, directExpenses, fixedExpenses, purchases, filter);
    }

    public static BudgetStatus Calculate(
        Guid accountId,
        YearMonth month,
        IEnumerable<CategoryBudget> budgets,
        IEnumerable<DirectExpense> directExpenses,
        IEnumerable<FixedExpense> fixedExpenses,
        IEnumerable<CreditCardPurchase> purchases,
        SpendingFilter filter)
    {
        DomainValidation.EnsureNotEmpty(accountId, nameof(accountId), "account id");
        ArgumentNullException.ThrowIfNull(month);
        ArgumentNullException.ThrowIfNull(budgets);
        ArgumentNullException.ThrowIfNull(directExpenses);
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(purchases);
        ArgumentNullException.ThrowIfNull(filter);

        var activeBudgets = CollectActiveBudgets(accountId, month, budgets);
        var spending = CategorySpendingCalculator.Calculate(
            accountId, month, directExpenses, fixedExpenses, purchases, filter);

        var spentByCategory = spending.Categories.ToDictionary(item => item.CategoryId, item => item.Total);

        var items = activeBudgets
            .Select(entry => new BudgetStatusItem(
                entry.Key,
                entry.Value,
                spentByCategory.GetValueOrDefault(entry.Key)))
            .OrderByDescending(item => item.UsagePercentage)
            .ThenByDescending(item => item.SpentAmount)
            .ThenBy(item => item.CategoryId)
            .ToList();

        var totalBudgeted = Money.Sum(items.Select(item => item.BudgetAmount));
        var totalSpent = Money.Sum(items.Select(item => item.SpentAmount));

        return new BudgetStatus(month, items.AsReadOnly(), totalBudgeted, totalSpent);
    }

    private static Dictionary<Guid, Money> CollectActiveBudgets(
        Guid accountId,
        YearMonth month,
        IEnumerable<CategoryBudget> budgets)
    {
        var activeBudgets = new Dictionary<Guid, Money>();
        foreach (var budget in budgets)
        {
            if (budget.AccountId != accountId || budget.AmountIn(month) is not { } amount)
            {
                continue;
            }

            if (!activeBudgets.TryAdd(budget.CategoryId, amount))
            {
                throw new DomainException(
                    "Multiple budgets are active for the same category in this month.");
            }
        }

        return activeBudgets;
    }
}
