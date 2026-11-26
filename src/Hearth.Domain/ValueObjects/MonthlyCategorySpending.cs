namespace Hearth.Domain.ValueObjects;

public enum CategoryExpenseOrigin
{
    DirectExpense = 0,
    FixedExpense = 1,
    CardInstallment = 2,
}

public sealed record CategoryExpenseEntry(
    Guid EntryId,
    string Description,
    DateOnly Date,
    Money Amount,
    CategoryExpenseOrigin Origin,
    int? InstallmentNumber = null,
    int? InstallmentCount = null);

public sealed record MonthlyCategorySpendingItem(
    Guid CategoryId,
    Money AmountSpent,
    decimal ShareOfTotalSpending,
    Money? BudgetAmount,
    decimal? BudgetUsagePercentage,
    IReadOnlyList<CategoryExpenseEntry> Entries);

public sealed record MonthlyCategorySpending(
    YearMonth Month,
    Money TotalSpent,
    IReadOnlyList<MonthlyCategorySpendingItem> Items);
