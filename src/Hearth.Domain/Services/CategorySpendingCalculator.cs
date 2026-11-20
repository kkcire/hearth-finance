using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Services;

/// <summary>
/// Calculates the spending of an account in a month grouped by category id,
/// using the same accrual rules as <see cref="ProjectedResultCalculator"/>.
/// </summary>
public static class CategorySpendingCalculator
{
    /// <summary>
    /// Groups direct expenses, active fixed expenses, and credit card installments of the cycle
    /// closing in <paramref name="month"/> by category. Only items of the given account are considered.
    /// </summary>
    /// <remarks>
    /// Source flags (<paramref name="includeDirectExpenses"/>, <paramref name="includeFixedExpenses"/>,
    /// <paramref name="includeCardInstallments"/>) and <paramref name="includedConditions"/> are
    /// orthogonal: <paramref name="includedConditions"/> applies exclusively to credit card purchases.
    /// When <paramref name="includedConditions"/> is null, every condition is included; when empty,
    /// no card installments are included. When all sources are enabled and <paramref name="includedConditions"/>
    /// is null, <see cref="CategorySpending.Total"/> reconciles with the expenses in
    /// <see cref="ProjectedResultCalculator"/>.
    /// </remarks>
    public static CategorySpending Calculate(
        Guid accountId,
        YearMonth month,
        IEnumerable<DirectExpense> directExpenses,
        IEnumerable<FixedExpense> fixedExpenses,
        IEnumerable<CreditCardPurchase> purchases,
        bool includeDirectExpenses = true,
        bool includeFixedExpenses = true,
        bool includeCardInstallments = true,
        IReadOnlyCollection<PurchaseCondition>? includedConditions = null)
    {
        DomainValidation.EnsureNotEmpty(accountId, nameof(accountId), "account id");
        ArgumentNullException.ThrowIfNull(month);
        ArgumentNullException.ThrowIfNull(directExpenses);
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(purchases);

        HashSet<PurchaseCondition>? conditionSet = null;
        if (includedConditions is not null)
        {
            conditionSet = new HashSet<PurchaseCondition>(includedConditions.Count);
            foreach (var condition in includedConditions)
            {
                if (condition is null)
                {
                    throw new ArgumentException(
                        "The included conditions must not contain null entries.",
                        nameof(includedConditions));
                }

                conditionSet.Add(condition);
            }
        }

        var directByCategory = new Dictionary<Guid, Money>();
        var fixedByCategory = new Dictionary<Guid, Money>();
        var cardByCategory = new Dictionary<Guid, Money>();
        var seenCategories = new HashSet<Guid>();

        if (includeDirectExpenses)
        {
            foreach (var expense in directExpenses)
            {
                if (expense.AccountId == accountId && expense.Month == month)
                {
                    AddAmount(directByCategory, seenCategories, expense.CategoryId, expense.Amount);
                }
            }
        }

        if (includeFixedExpenses)
        {
            foreach (var expense in fixedExpenses)
            {
                if (expense.AccountId == accountId && expense.AmountIn(month) is { } amount)
                {
                    AddAmount(fixedByCategory, seenCategories, expense.CategoryId, amount);
                }
            }
        }

        if (includeCardInstallments && (conditionSet is null || conditionSet.Count > 0))
        {
            foreach (var purchase in purchases)
            {
                if (purchase.AccountId != accountId)
                {
                    continue;
                }

                if (conditionSet is not null && !conditionSet.Contains(purchase.Condition))
                {
                    continue;
                }

                foreach (var installment in purchase.GetInstallments())
                {
                    if (!installment.IsPreviouslyPaid && installment.Cycle.ClosingMonth == month)
                    {
                        AddAmount(cardByCategory, seenCategories, purchase.CategoryId, installment.Amount);
                    }
                }
            }
        }

        var items = seenCategories
            .Select(categoryId => new CategorySpendingItem(
                categoryId,
                directByCategory.GetValueOrDefault(categoryId),
                fixedByCategory.GetValueOrDefault(categoryId),
                cardByCategory.GetValueOrDefault(categoryId)))
            .Where(item => item.Total > Money.Zero)
            .OrderByDescending(item => item.Total)
            .ThenBy(item => item.CategoryId)
            .ToList();

        var totalDirect = Money.Sum(items.Select(item => item.DirectExpenses));
        var totalFixed = Money.Sum(items.Select(item => item.FixedExpenses));
        var totalCard = Money.Sum(items.Select(item => item.CardInstallments));

        return new CategorySpending(month, items.AsReadOnly(), totalDirect, totalFixed, totalCard);
    }

    private static void AddAmount(
        Dictionary<Guid, Money> totals,
        HashSet<Guid> seenCategories,
        Guid categoryId,
        Money amount)
    {
        seenCategories.Add(categoryId);
        totals[categoryId] = totals.GetValueOrDefault(categoryId) + amount;
    }
}
