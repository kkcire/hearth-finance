using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Services;

/// <summary>
/// Calculates the spending of an account in a month grouped by category id,
/// using the same accrual rules as <see cref="ProjectedResultCalculator"/>.
/// </summary>
public static class CategorySpendingCalculator
{
    public static CategorySpending Calculate(
        Guid accountId,
        YearMonth month,
        IEnumerable<DirectExpense> directExpenses,
        IEnumerable<FixedExpense> fixedExpenses,
        IEnumerable<CreditCardPurchase> purchases,
        bool includeDirectExpenses = true,
        bool includeFixedExpenses = true,
        bool includeCardInstallments = true,
        IReadOnlyCollection<Guid>? includedConditionIds = null)
    {
        var filter = SpendingFilter.Create(
            includeDirectExpenses,
            includeFixedExpenses,
            includeCardInstallments,
            includedConditionIds);

        return Calculate(accountId, month, directExpenses, fixedExpenses, purchases, filter);
    }

    public static CategorySpending Calculate(
        Guid accountId,
        YearMonth month,
        IEnumerable<DirectExpense> directExpenses,
        IEnumerable<FixedExpense> fixedExpenses,
        IEnumerable<CreditCardPurchase> purchases,
        SpendingFilter filter)
    {
        DomainValidation.EnsureNotEmpty(accountId, nameof(accountId), "account id");
        ArgumentNullException.ThrowIfNull(month);
        ArgumentNullException.ThrowIfNull(directExpenses);
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(purchases);
        ArgumentNullException.ThrowIfNull(filter);

        var accumulator = new CategoryAccumulator();

        if (filter.IncludeDirectExpenses)
        {
            AccumulateDirectExpenses(accumulator, accountId, month, directExpenses);
        }

        if (filter.IncludeFixedExpenses)
        {
            AccumulateFixedExpenses(accumulator, accountId, month, fixedExpenses);
        }

        if (filter.ShouldProcessCardInstallments)
        {
            AccumulateCardInstallments(accumulator, accountId, month, purchases, filter);
        }

        return accumulator.Build(month);
    }

    private static void AccumulateDirectExpenses(
        CategoryAccumulator accumulator,
        Guid accountId,
        YearMonth month,
        IEnumerable<DirectExpense> directExpenses)
    {
        foreach (var expense in directExpenses)
        {
            if (expense.AccountId == accountId && expense.Month == month)
            {
                accumulator.AddDirect(expense.CategoryId, expense.Amount);
            }
        }
    }

    private static void AccumulateFixedExpenses(
        CategoryAccumulator accumulator,
        Guid accountId,
        YearMonth month,
        IEnumerable<FixedExpense> fixedExpenses)
    {
        foreach (var expense in fixedExpenses)
        {
            if (expense.AccountId == accountId && expense.AmountIn(month) is { } amount)
            {
                accumulator.AddFixed(expense.CategoryId, amount);
            }
        }
    }

    private static void AccumulateCardInstallments(
        CategoryAccumulator accumulator,
        Guid accountId,
        YearMonth month,
        IEnumerable<CreditCardPurchase> purchases,
        SpendingFilter filter)
    {
        foreach (var purchase in purchases)
        {
            if (purchase.AccountId != accountId || !filter.MatchesCondition(purchase.ConditionId))
            {
                continue;
            }

            foreach (var installment in purchase.GetActiveInstallments())
            {
                if (installment.Cycle.ClosingMonth == month)
                {
                    accumulator.AddCard(purchase.CategoryId, installment.Amount);
                }
            }
        }
    }

    private sealed class CategoryAccumulator
    {
        private readonly Dictionary<Guid, Money> _directByCategory = [];
        private readonly Dictionary<Guid, Money> _fixedByCategory = [];
        private readonly Dictionary<Guid, Money> _cardByCategory = [];
        private readonly HashSet<Guid> _seenCategories = [];

        public void AddDirect(Guid categoryId, Money amount) => Add(_directByCategory, categoryId, amount);

        public void AddFixed(Guid categoryId, Money amount) => Add(_fixedByCategory, categoryId, amount);

        public void AddCard(Guid categoryId, Money amount) => Add(_cardByCategory, categoryId, amount);

        public CategorySpending Build(YearMonth month)
        {
            var items = _seenCategories
                .Select(categoryId => new CategorySpendingItem(
                    categoryId,
                    _directByCategory.GetValueOrDefault(categoryId),
                    _fixedByCategory.GetValueOrDefault(categoryId),
                    _cardByCategory.GetValueOrDefault(categoryId)))
                .Where(item => item.Total > Money.Zero)
                .OrderByDescending(item => item.Total)
                .ThenBy(item => item.CategoryId)
                .ToList();

            var totalDirect = Money.Sum(items.Select(item => item.DirectExpenses));
            var totalFixed = Money.Sum(items.Select(item => item.FixedExpenses));
            var totalCard = Money.Sum(items.Select(item => item.CardInstallments));

            return new CategorySpending(month, items.AsReadOnly(), totalDirect, totalFixed, totalCard);
        }

        private void Add(Dictionary<Guid, Money> totals, Guid categoryId, Money amount)
        {
            _seenCategories.Add(categoryId);
            totals[categoryId] = totals.GetValueOrDefault(categoryId) + amount;
        }
    }
}
