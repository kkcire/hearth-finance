using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Reports.GetBudgetStatus;

public sealed record BudgetStatusItemDto(
    Guid CategoryId,
    string CategoryName,
    decimal BudgetAmount,
    decimal SpentAmount,
    decimal RemainingAmount,
    bool IsExceeded,
    decimal UsagePercentage);

public sealed record BudgetStatusDto(
    Guid AccountId,
    int Year,
    int Month,
    decimal TotalBudgeted,
    decimal TotalSpent,
    decimal TotalRemaining,
    IReadOnlyList<BudgetStatusItemDto> Items)
{
    internal static BudgetStatusDto FromDomain(
        Guid accountId,
        BudgetStatus status,
        IReadOnlyDictionary<Guid, string> categoryNames) =>
        new(
            accountId,
            status.Month.Year,
            status.Month.Month,
            status.TotalBudgeted.Amount,
            status.TotalSpent.Amount,
            status.TotalRemaining.Amount,
            status.Items
                .Select(item => new BudgetStatusItemDto(
                    item.CategoryId,
                    categoryNames.GetValueOrDefault(item.CategoryId, string.Empty),
                    item.BudgetAmount.Amount,
                    item.SpentAmount.Amount,
                    item.RemainingAmount.Amount,
                    item.IsExceeded,
                    item.UsagePercentage))
                .OrderByDescending(item => item.UsagePercentage)
                .ThenByDescending(item => item.SpentAmount)
                .ThenBy(item => item.CategoryName, StringComparer.OrdinalIgnoreCase)
                .ToList());
}
