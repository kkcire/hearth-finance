using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Reports.GetExpensesByCategory;

public sealed record CategorySpendingItemDto(
    Guid CategoryId,
    string CategoryName,
    decimal DirectExpenses,
    decimal FixedExpenses,
    decimal CardInstallments,
    decimal Total);

public sealed record CategorySpendingDto(
    Guid AccountId,
    int Year,
    int Month,
    decimal DirectExpenses,
    decimal FixedExpenses,
    decimal CardInstallments,
    decimal Total,
    IReadOnlyList<CategorySpendingItemDto> Categories)
{
    internal static CategorySpendingDto FromDomain(
        Guid accountId,
        CategorySpending spending,
        IReadOnlyDictionary<Guid, string> categoryNames) =>
        new(
            accountId,
            spending.Month.Year,
            spending.Month.Month,
            spending.DirectExpenses.Amount,
            spending.FixedExpenses.Amount,
            spending.CardInstallments.Amount,
            spending.Total.Amount,
            spending.Categories
                .Select(item => new CategorySpendingItemDto(
                    item.CategoryId,
                    categoryNames.GetValueOrDefault(item.CategoryId, string.Empty),
                    item.DirectExpenses.Amount,
                    item.FixedExpenses.Amount,
                    item.CardInstallments.Amount,
                    item.Total.Amount))
                .OrderByDescending(item => item.Total)
                .ThenBy(item => item.CategoryName, StringComparer.OrdinalIgnoreCase)
                .ToList());
}
