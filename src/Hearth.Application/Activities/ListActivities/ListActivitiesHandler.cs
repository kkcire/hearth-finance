using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.PurchaseConditions;
using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Activities.ListActivities;

public sealed class ListActivitiesHandler
{
    public const string IncomeType = "Income";
    public const string DirectExpenseType = "DirectExpense";
    public const string FixedExpenseType = "FixedExpense";
    public const string CardPurchaseType = "CardPurchase";

    private static readonly IReadOnlyDictionary<Guid, string> SystemCategoryIcons =
        new Dictionary<Guid, string>
        {
            [SystemCategories.FoodId] = "food",
            [SystemCategories.ServicesId] = "services",
            [SystemCategories.TransportId] = "transport",
            [SystemCategories.ShoppingId] = "shopping",
            [SystemCategories.HealthId] = "health",
            [SystemCategories.EducationId] = "education",
            [SystemCategories.EntertainmentId] = "entertainment",
            [SystemCategories.PersonalId] = "personal",
        };

    private readonly AccountAccessGuard _accountAccess;
    private readonly ICategoryRepository _categories;
    private readonly IIncomeRepository _incomes;
    private readonly IDirectExpenseRepository _directExpenses;
    private readonly IFixedExpenseRepository _fixedExpenses;
    private readonly ICreditCardPurchaseRepository _purchases;
    private readonly IClock _clock;

    public ListActivitiesHandler(
        AccountAccessGuard accountAccess,
        ICategoryRepository categories,
        IIncomeRepository incomes,
        IDirectExpenseRepository directExpenses,
        IFixedExpenseRepository fixedExpenses,
        ICreditCardPurchaseRepository purchases,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(incomes);
        ArgumentNullException.ThrowIfNull(directExpenses);
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(purchases);
        ArgumentNullException.ThrowIfNull(clock);

        _accountAccess = accountAccess;
        _categories = categories;
        _incomes = incomes;
        _directExpenses = directExpenses;
        _fixedExpenses = fixedExpenses;
        _purchases = purchases;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ActivityItemDto>> ExecuteAsync(
        ListActivitiesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        PurchaseConditionValidation.EnsureOptionalFilterNotEmpty(query.ConditionId, nameof(query));

        YearMonth? filterMonth = null;
        if (query.Year.HasValue || query.Month.HasValue)
        {
            if (!query.Year.HasValue || !query.Month.HasValue)
            {
                throw new ArgumentException("Both Year and Month must be provided together when filtering by month.", nameof(query));
            }

            filterMonth = YearMonth.Of(query.Year.Value, query.Month.Value);
        }

        var normalizedType = string.IsNullOrWhiteSpace(query.MovementType)
            ? null
            : query.MovementType.Trim();

        var account = await _accountAccess.GetAuthorizedAccountAsync(query.AccountId, cancellationToken);
        var categories = await _categories.GetVisibleForUserAsync(account.OwnerUserId, cancellationToken);
        var categoryNames = categories.ToDictionary(c => c.Id, c => c.Name);

        var today = _clock.Today;
        var currentMonth = YearMonth.From(today);
        var initialMonth = YearMonth.From(account.InitialBalanceDate);

        var items = new List<ActivityItemDto>();

        if (MatchesType(normalizedType, IncomeType))
        {
            var incomes = await _incomes.GetByAccountAsync(account.Id, cancellationToken);
            foreach (var income in incomes)
            {
                if (income.AccountId != account.Id
                    || (query.ConditionId.HasValue && income.ConditionId != query.ConditionId.Value))
                {
                    continue;
                }

                var effectiveDate = income.ReceivedOn ?? income.ExpectedDate;
                if (filterMonth is not null && YearMonth.From(effectiveDate) != filterMonth)
                {
                    continue;
                }

                items.Add(new ActivityItemDto(
                    income.Id,
                    income.Description,
                    effectiveDate,
                    income.Amount.Amount,
                    IncomeType,
                    "Receita",
                    "income",
                    income.IsReceived ? "Efetivado" : "Previsto",
                    null));
            }
        }

        if (MatchesType(normalizedType, DirectExpenseType))
        {
            var directExpenses = await _directExpenses.GetByAccountAsync(account.Id, cancellationToken);
            foreach (var expense in directExpenses)
            {
                if (expense.AccountId != account.Id)
                {
                    continue;
                }

                if (filterMonth is not null && expense.Month != filterMonth)
                {
                    continue;
                }

                items.Add(new ActivityItemDto(
                    expense.Id,
                    expense.Description,
                    expense.Date,
                    -expense.Amount.Amount,
                    DirectExpenseType,
                    categoryNames.GetValueOrDefault(expense.CategoryId, "Outros"),
                    ResolveCategoryIcon(expense.CategoryId),
                    expense.Date <= today ? "Efetivado" : "Previsto",
                    null));
            }
        }

        if (MatchesType(normalizedType, FixedExpenseType))
        {
            var fixedExpenses = await _fixedExpenses.GetByAccountAsync(account.Id, cancellationToken);
            foreach (var expense in fixedExpenses)
            {
                if (expense.AccountId != account.Id)
                {
                    continue;
                }

                var categoryName = categoryNames.GetValueOrDefault(expense.CategoryId, "Outros");

                if (filterMonth is not null)
                {
                    if (expense.AmountIn(filterMonth) is { } amount)
                    {
                        var paidOn = expense.PaidOn(filterMonth);
                        items.Add(new ActivityItemDto(
                            expense.Id,
                            expense.Description,
                            paidOn ?? filterMonth.FirstDay,
                            -amount.Amount,
                            FixedExpenseType,
                            categoryName,
                            "fixed",
                            paidOn.HasValue ? "Efetivado" : "Previsto",
                            null));
                    }
                }
                else
                {
                    foreach (var month in EnumerateFixedExpenseMonths(expense, initialMonth, currentMonth))
                    {
                        if (expense.AmountIn(month) is not { } amount)
                        {
                            continue;
                        }

                        var paidOn = expense.PaidOn(month);
                        items.Add(new ActivityItemDto(
                            expense.Id,
                            expense.Description,
                            paidOn ?? month.FirstDay,
                            -amount.Amount,
                            FixedExpenseType,
                            categoryName,
                            "fixed",
                            paidOn.HasValue ? "Efetivado" : "Previsto",
                            null));
                    }
                }
            }
        }

        if (MatchesType(normalizedType, CardPurchaseType))
        {
            var purchases = await _purchases.GetByAccountAsync(account.Id, cancellationToken);
            foreach (var purchase in purchases)
            {
                if (purchase.AccountId != account.Id
                    || (query.ConditionId.HasValue && purchase.ConditionId != query.ConditionId.Value))
                {
                    continue;
                }

                var categoryName = categoryNames.GetValueOrDefault(purchase.CategoryId, "Outros");

                foreach (var installment in purchase.GetTrackedInstallments(account.TrackingStartDate))
                {
                    if (filterMonth is not null && installment.Cycle.DueMonth != filterMonth)
                    {
                        continue;
                    }

                    var installmentInfo = purchase.InstallmentCount > 1
                        ? $"{installment.Number}/{purchase.InstallmentCount}"
                        : null;
                    var statusLabel = installment.Cycle.GetStatus(today) == InvoiceStatus.Paid
                        ? "Efetivado"
                        : "Previsto";

                    items.Add(new ActivityItemDto(
                        purchase.Id,
                        purchase.Description,
                        installment.Cycle.DueDate,
                        -installment.Amount.Amount,
                        CardPurchaseType,
                        categoryName,
                        "card",
                        statusLabel,
                        installmentInfo));
                }
            }
        }

        return items
            .OrderByDescending(item => item.Date)
            .ThenByDescending(item => Math.Abs(item.Amount))
            .ThenBy(item => item.Description, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<YearMonth> EnumerateFixedExpenseMonths(
        FixedExpense expense,
        YearMonth initialMonth,
        YearMonth currentMonth)
    {
        var start = expense.StartMonth < initialMonth ? initialMonth : expense.StartMonth;

        var latestPaidMonth = expense.Payments.Keys
            .DefaultIfEmpty()
            .Max();

        YearMonth end;
        if (expense.EndMonth is { } endMonth)
        {
            end = endMonth;
        }
        else
        {
            end = currentMonth;
            if (start > end)
            {
                end = start;
            }
            if (latestPaidMonth is not null && latestPaidMonth > end)
            {
                end = latestPaidMonth;
            }
        }

        if (start > end)
        {
            yield break;
        }

        var cursor = start;
        while (true)
        {
            yield return cursor;
            if (cursor == end)
            {
                break;
            }

            cursor = cursor.AddMonths(1);
        }
    }

    private static bool MatchesType(string? requestedType, string candidateType) =>
        requestedType is null || string.Equals(requestedType, candidateType, StringComparison.OrdinalIgnoreCase);

    private static string ResolveCategoryIcon(Guid categoryId) =>
        SystemCategoryIcons.TryGetValue(categoryId, out var icon) ? icon : "tag";
}
