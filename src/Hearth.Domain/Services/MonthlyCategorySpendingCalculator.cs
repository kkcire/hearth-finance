using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Services;

/// <summary>
/// Pure domain calculator for monthly category spending distribution and itemized composition.
/// Aggregates direct expenses, active fixed expenses, and active credit card installments
/// unified on <see cref="BillingCycle.DueMonth"/> with <c>DueDate &gt;= TrackingStartDate</c>.
/// </summary>
public static class MonthlyCategorySpendingCalculator
{
    public static MonthlyCategorySpending Calculate(
        Account account,
        YearMonth month,
        IEnumerable<CategoryBudget> budgets,
        IEnumerable<DirectExpense> directExpenses,
        IEnumerable<FixedExpense> fixedExpenses,
        IEnumerable<CreditCardPurchase> purchases,
        Guid? conditionId = null)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(month);
        ArgumentNullException.ThrowIfNull(budgets);
        ArgumentNullException.ThrowIfNull(directExpenses);
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(purchases);
        DomainValidation.EnsureOptionalNotEmpty(conditionId, nameof(conditionId), "condition id");

        if (!account.CanRegisterInMonth(month))
        {
            return new MonthlyCategorySpending(month, Money.Zero, []);
        }

        var accountId = account.Id;
        var spentByCategory = new Dictionary<Guid, Money>();
        var entriesByCategory = new Dictionary<Guid, List<CategoryExpenseEntry>>();

        static void AddEntry(
            Dictionary<Guid, Money> totals,
            Dictionary<Guid, List<CategoryExpenseEntry>> entries,
            Guid categoryId,
            CategoryExpenseEntry entry)
        {
            totals[categoryId] = totals.GetValueOrDefault(categoryId) + entry.Amount;
            if (!entries.TryGetValue(categoryId, out var list))
            {
                list = [];
                entries[categoryId] = list;
            }

            list.Add(entry);
        }

        foreach (var expense in directExpenses)
        {
            if (expense.AccountId == accountId
                && expense.Month == month
                && account.CanRegisterOn(expense.Date))
            {
                AddEntry(spentByCategory, entriesByCategory, expense.CategoryId, new CategoryExpenseEntry(
                    expense.Id,
                    expense.Description,
                    expense.Date,
                    expense.Amount,
                    CategoryExpenseOrigin.DirectExpense));
            }
        }

        foreach (var expense in fixedExpenses)
        {
            if (expense.AccountId == accountId && expense.AmountIn(month) is { } amount)
            {
                AddEntry(spentByCategory, entriesByCategory, expense.CategoryId, new CategoryExpenseEntry(
                    expense.Id,
                    expense.Description,
                    expense.PaidOn(month) ?? month.FirstDay,
                    amount,
                    CategoryExpenseOrigin.FixedExpense));
            }
        }

        foreach (var purchase in purchases)
        {
            if (purchase.AccountId != accountId
                || (conditionId.HasValue && purchase.ConditionId != conditionId.Value))
            {
                continue;
            }

            foreach (var installment in purchase.GetTrackedInstallments(account.TrackingStartDate))
            {
                if (installment.Cycle.DueMonth == month)
                {
                    AddEntry(spentByCategory, entriesByCategory, purchase.CategoryId, new CategoryExpenseEntry(
                        purchase.Id,
                        purchase.Description,
                        purchase.Date,
                        installment.Amount,
                        CategoryExpenseOrigin.CardInstallment,
                        purchase.InstallmentCount > 1 ? installment.Number : null,
                        purchase.InstallmentCount > 1 ? purchase.InstallmentCount : null));
                }
            }
        }

        var budgetsByCategory = new Dictionary<Guid, Money>();
        foreach (var budget in budgets)
        {
            if (budget.AccountId == accountId && budget.AmountIn(month) is { } budgetAmount)
            {
                budgetsByCategory.TryAdd(budget.CategoryId, budgetAmount);
            }
        }

        var totalSpent = Money.Sum(spentByCategory.Values);
        if (totalSpent <= Money.Zero)
        {
            return new MonthlyCategorySpending(month, Money.Zero, []);
        }

        var items = spentByCategory
            .Where(kv => kv.Value > Money.Zero)
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Select(kv =>
            {
                var share = Math.Round((kv.Value.Amount / totalSpent.Amount) * 100m, 1, MidpointRounding.AwayFromZero);
                Money? budgetAmount = budgetsByCategory.TryGetValue(kv.Key, out var b) ? b : null;
                decimal? budgetUsage = budgetAmount is { Amount: > 0m }
                    ? Math.Round((kv.Value.Amount / budgetAmount.Value.Amount) * 100m, 1, MidpointRounding.AwayFromZero)
                    : null;

                IReadOnlyList<CategoryExpenseEntry> orderedEntries = entriesByCategory.TryGetValue(kv.Key, out var rawEntries)
                    ? rawEntries
                        .OrderByDescending(e => e.Amount)
                        .ThenByDescending(e => e.Date)
                        .ThenBy(e => e.Description, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                        .AsReadOnly()
                    : [];

                return new MonthlyCategorySpendingItem(
                    kv.Key,
                    kv.Value,
                    share,
                    budgetAmount,
                    budgetUsage,
                    orderedEntries);
            })
            .ToList()
            .AsReadOnly();

        return new MonthlyCategorySpending(month, totalSpent, items);
    }
}
