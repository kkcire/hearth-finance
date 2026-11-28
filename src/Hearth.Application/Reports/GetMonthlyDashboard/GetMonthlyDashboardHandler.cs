using Hearth.Application.Abstractions;
using Hearth.Application.Accounts;
using Hearth.Application.Authorization;
using Hearth.Application.PurchaseConditions;
using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.Services;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Reports.GetMonthlyDashboard;

public sealed class GetMonthlyDashboardHandler
{
    private const int MaxRecentActivities = 6;

    private static readonly IReadOnlyDictionary<Guid, (string HexColor, string IconKey)> SystemCategoryStyles =
        new Dictionary<Guid, (string HexColor, string IconKey)>
        {
            [SystemCategories.FoodId] = ("#3CC47C", "food"),
            [SystemCategories.ServicesId] = ("#4C8DF6", "services"),
            [SystemCategories.TransportId] = ("#7A77F5", "transport"),
            [SystemCategories.ShoppingId] = ("#E86F51", "shopping"),
            [SystemCategories.HealthId] = ("#2EC4B6", "health"),
            [SystemCategories.EducationId] = ("#38BDF8", "education"),
            [SystemCategories.EntertainmentId] = ("#E5A93B", "entertainment"),
            [SystemCategories.PersonalId] = ("#D95F97", "personal"),
        };

    private static readonly string[] CustomCategoryPalette =
    [
        "#3CC47C",
        "#4C8DF6",
        "#E86F51",
        "#2EC4B6",
        "#E5A93B",
        "#7A77F5",
        "#D95F97",
        "#84C96C",
    ];

    private readonly AccountAccessGuard _accountAccess;
    private readonly ICreditCardRepository _creditCards;
    private readonly ICategoryRepository _categories;
    private readonly ICategoryBudgetRepository _budgets;
    private readonly IIncomeRepository _incomes;
    private readonly IFixedIncomeRepository _fixedIncomes;
    private readonly IDirectExpenseRepository _directExpenses;
    private readonly IFixedExpenseRepository _fixedExpenses;
    private readonly ICreditCardPurchaseRepository _purchases;
    private readonly IClock _clock;

    public GetMonthlyDashboardHandler(
        AccountAccessGuard accountAccess,
        ICreditCardRepository creditCards,
        ICategoryRepository categories,
        ICategoryBudgetRepository budgets,
        IIncomeRepository incomes,
        IFixedIncomeRepository fixedIncomes,
        IDirectExpenseRepository directExpenses,
        IFixedExpenseRepository fixedExpenses,
        ICreditCardPurchaseRepository purchases,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(creditCards);
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(budgets);
        ArgumentNullException.ThrowIfNull(incomes);
        ArgumentNullException.ThrowIfNull(fixedIncomes);
        ArgumentNullException.ThrowIfNull(directExpenses);
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(purchases);
        ArgumentNullException.ThrowIfNull(clock);

        _accountAccess = accountAccess;
        _creditCards = creditCards;
        _categories = categories;
        _budgets = budgets;
        _incomes = incomes;
        _fixedIncomes = fixedIncomes;
        _directExpenses = directExpenses;
        _fixedExpenses = fixedExpenses;
        _purchases = purchases;
        _clock = clock;
    }

    public async Task<MonthlyDashboardDto> ExecuteAsync(
        GetMonthlyDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.Month);
        PurchaseConditionValidation.EnsureOptionalFilterNotEmpty(query.ConditionId, nameof(query));

        var today = _clock.Today;
        var account = await _accountAccess.GetAuthorizedAccountWithBalanceHistoryAsync(query.AccountId, cancellationToken);
        var asOfDate = query.BalanceAsOfDate is { } requested && requested < today ? requested : today;
        if (today >= account.TrackingStartDate && asOfDate < account.TrackingStartDate)
        {
            asOfDate = account.TrackingStartDate;
        }
        var activeCard = await _creditCards.GetActiveByAccountAsync(account.Id, cancellationToken);
        var categories = await _categories.GetVisibleForUserAsync(account.OwnerUserId, cancellationToken);
        var budgets = await _budgets.GetByAccountAsync(account.Id, cancellationToken);
        var incomes = await _incomes.GetByAccountAsync(account.Id, cancellationToken);
        var fixedIncomes = await _fixedIncomes.GetByAccountAsync(account.Id, cancellationToken);
        var directExpenses = await _directExpenses.GetByAccountAsync(account.Id, cancellationToken);
        var fixedExpenses = await _fixedExpenses.GetByAccountAsync(account.Id, cancellationToken);
        var purchases = await _purchases.GetByAccountAsync(account.Id, cancellationToken);

        var snapshot = MonthlyDashboardCalculator.Calculate(
            account,
            query.Month,
            asOfDate,
            today,
            activeCard,
            incomes,
            directExpenses,
            fixedExpenses,
            purchases,
            query.ConditionId,
            query.HistoryMonths,
            fixedIncomes);

        var categoryNames = categories.ToDictionary(c => c.Id, c => c.Name);

        var categorySpending = BuildCategorySpending(
            account,
            query.Month,
            query.ConditionId,
            categoryNames,
            budgets,
            directExpenses,
            fixedExpenses,
            purchases);

        var recentActivities = BuildRecentActivities(
            account,
            query.Month,
            today,
            query.ConditionId,
            directExpenses,
            fixedExpenses,
            purchases);

        var incomeEntries = BuildIncomeEntries(account, query.Month, today, query.ConditionId, incomes, fixedIncomes);

        var balanceHistory = account.BalanceAdjustments
            .OrderByDescending(entry => entry.CreatedAt)
            .Select(BalanceAdjustmentDto.FromEntity)
            .ToList();

        return new MonthlyDashboardDto(
            account.Id,
            query.Month,
            query.ConditionId,
            new DashboardBalanceDto(
                snapshot.Balance.Balance.Amount,
                snapshot.Balance.Until,
                snapshot.Balance.InitialBalance.Amount,
                snapshot.Balance.Incomes.Amount,
                snapshot.Balance.DirectExpenses.Amount,
                snapshot.Balance.FixedExpenses.Amount,
                snapshot.Balance.CardInvoices.Amount,
                balanceHistory),
            new DashboardInvoiceDto(
                snapshot.CurrentInvoice.BillingMonth,
                snapshot.CurrentInvoice.CycleAmount.Amount,
                snapshot.CurrentInvoice.DueDate,
                snapshot.CurrentInvoice.ClosingDate,
                snapshot.CurrentInvoice.Status,
                snapshot.CurrentInvoice.FutureInstallmentsTotal.Amount,
                snapshot.CurrentInvoice.HasActiveCard,
                activeCard?.Name),
            new DashboardCurrentMonthFlowDto(
                snapshot.CurrentMonthFlow.MonthlyIncome.Amount,
                snapshot.CurrentMonthFlow.TotalExpenses.Amount,
                snapshot.CurrentMonthFlow.DirectExpenses.Amount,
                snapshot.CurrentMonthFlow.FixedExpenses.Amount,
                snapshot.CurrentMonthFlow.CardInvoice.Amount,
                snapshot.CurrentMonthFlow.MonthlyResult.Amount,
                snapshot.CurrentMonthFlow.SavingsRatePercentage,
                snapshot.CurrentMonthFlow.BreakEvenStatus,
                snapshot.CurrentMonthFlow.CommittedFloor.Amount,
                incomeEntries,
                snapshot.CurrentMonthFlow.OwnMonthlyIncome.Amount),
            snapshot.MonthlyResultHistory
                .Select(point => new DashboardMonthlyResultPointDto(
                    point.Month,
                    point.MonthlyIncome.Amount,
                    point.TotalExpenses.Amount,
                    point.MonthlyResult.Amount,
                    point.BreakEvenStatus,
                    point.IsProjection))
                .ToList(),
            categorySpending,
            recentActivities,
            snapshot.NextMonthEstimate is { } next
                ? new DashboardNextMonthEstimateDto(
                    next.NextYearMonth,
                    next.EstimatedIncome.Amount,
                    next.EstimatedFixedExpenses.Amount,
                    next.EstimatedCardInstallments.Amount,
                    next.EstimatedDirectExpenses.Amount,
                    next.EstimatedCommitments.Amount,
                    next.ProjectedResult.Amount,
                    next.EstimatedSavingsRatePercentage,
                    next.ProjectedOpeningBalance?.Amount,
                    next.ProjectedEndingBalance?.Amount)
                : null);
    }

    private static IReadOnlyList<DashboardIncomeEntryDto> BuildIncomeEntries(
        Account account,
        YearMonth month,
        DateOnly today,
        Guid? conditionId,
        IReadOnlyCollection<Income> incomes,
        IReadOnlyCollection<FixedIncome> fixedIncomes)
    {
        if (!account.CanRegisterInMonth(month))
        {
            return [];
        }

        var entries = new List<DashboardIncomeEntryDto>();

        foreach (var income in incomes)
        {
            if (income.AccountId != account.Id
                || income.Month != month
                || !account.CanRegisterOn(income.Date)
                || (conditionId.HasValue && income.ConditionId != conditionId.Value))
            {
                continue;
            }

            entries.Add(new DashboardIncomeEntryDto(
                income.Id,
                income.Description,
                income.Date,
                income.Amount.Amount,
                income.IsReceivedOn(today)));
        }

        foreach (var fixedIncome in fixedIncomes)
        {
            if (fixedIncome.AccountId != account.Id
                || (conditionId.HasValue && fixedIncome.ConditionId != conditionId.Value))
            {
                continue;
            }

            var receipt = fixedIncome.GetReceiptFor(month);
            if (receipt is not null)
            {
                entries.Add(new DashboardIncomeEntryDto(
                    fixedIncome.Id,
                    fixedIncome.Description,
                    receipt.ReceivedOn,
                    receipt.ActualAmount.Amount,
                    true));
            }
            else if (fixedIncome.IsActiveIn(month))
            {
                entries.Add(new DashboardIncomeEntryDto(
                    fixedIncome.Id,
                    fixedIncome.Description,
                    fixedIncome.ExpectedDateIn(month),
                    fixedIncome.GetExpectedAmountFor(month).Amount,
                    false));
            }
        }

        return entries
            .OrderByDescending(entry => entry.Amount)
            .ThenBy(entry => entry.Date)
            .ThenBy(entry => entry.Description, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<DashboardCategorySpendingItemDto> BuildCategorySpending(
        Account account,
        YearMonth month,
        Guid? conditionId,
        IReadOnlyDictionary<Guid, string> categoryNames,
        IReadOnlyCollection<CategoryBudget> budgets,
        IReadOnlyCollection<DirectExpense> directExpenses,
        IReadOnlyCollection<FixedExpense> fixedExpenses,
        IReadOnlyCollection<CreditCardPurchase> purchases)
    {
        var spending = MonthlyCategorySpendingCalculator.Calculate(
            account,
            month,
            budgets,
            directExpenses,
            fixedExpenses,
            purchases,
            conditionId);

        return spending.Items
            .OrderByDescending(item => item.AmountSpent.Amount)
            .ThenBy(item => categoryNames.GetValueOrDefault(item.CategoryId, string.Empty), StringComparer.OrdinalIgnoreCase)
            .Select(item =>
            {
                var name = categoryNames.GetValueOrDefault(item.CategoryId, "Outros");
                var entries = item.Entries
                    .Select(entry => new DashboardCategoryExpenseEntryDto(
                        entry.EntryId,
                        entry.Description,
                        entry.Date,
                        entry.Amount.Amount,
                        entry.Origin switch
                        {
                            CategoryExpenseOrigin.DirectExpense => "Gasto direto",
                            CategoryExpenseOrigin.FixedExpense => "Despesa fixa",
                            CategoryExpenseOrigin.CardInstallment => "Cartão",
                            _ => "Despesa",
                        },
                        entry.InstallmentNumber.HasValue && entry.InstallmentCount.HasValue
                            ? $"{entry.InstallmentNumber.Value}/{entry.InstallmentCount.Value}"
                            : null))
                    .ToList();

                return new DashboardCategorySpendingItemDto(
                    item.CategoryId,
                    name,
                    ResolveCategoryColor(item.CategoryId),
                    item.AmountSpent.Amount,
                    item.ShareOfTotalSpending,
                    item.BudgetAmount?.Amount,
                    item.BudgetUsagePercentage,
                    entries);
            })
            .ToList();
    }

    private static IReadOnlyList<DashboardRecentActivityDto> BuildRecentActivities(
        Account account,
        YearMonth month,
        DateOnly today,
        Guid? conditionId,
        IReadOnlyCollection<DirectExpense> directExpenses,
        IReadOnlyCollection<FixedExpense> fixedExpenses,
        IReadOnlyCollection<CreditCardPurchase> purchases)
    {
        if (!account.CanRegisterInMonth(month))
        {
            return [];
        }

        var accountId = account.Id;
        var activities = new List<DashboardRecentActivityDto>();

        // 1. Gastos diretos realizados/agendados NAQUELE mês
        foreach (var expense in directExpenses)
        {
            if (expense.AccountId != accountId || expense.Month != month || !account.CanRegisterOn(expense.Date))
            {
                continue;
            }

            activities.Add(new DashboardRecentActivityDto(
                expense.Id,
                expense.Description,
                expense.Date,
                -expense.Amount.Amount,
                "DirectExpense",
                ResolveCategoryIcon(expense.CategoryId),
                expense.Date <= today ? "Efetivado" : "Previsto"));
        }

        // 2. Despesas fixas ativas NAQUELE mês
        foreach (var expense in fixedExpenses)
        {
            if (expense.AccountId != accountId || expense.AmountIn(month) is not { } amount)
            {
                continue;
            }

            var paidOn = expense.PaidOn(month);
            var date = paidOn ?? month.FirstDay;
            activities.Add(new DashboardRecentActivityDto(
                expense.Id,
                expense.Description,
                date,
                -amount.Amount,
                "FixedExpense",
                "fixed",
                paidOn.HasValue ? "Efetivado" : "Previsto"));
        }

        // 3. Compras no cartão feitas NAQUELE mês (respeitando condição)
        foreach (var purchase in purchases)
        {
            if (purchase.AccountId != accountId
                || (conditionId.HasValue && purchase.ConditionId != conditionId.Value)
                || YearMonth.From(purchase.Date) != month)
            {
                continue;
            }

            var eligibleInstallments = purchase.GetTrackedInstallments(account.TrackingStartDate)
                .ToList();

            if (eligibleInstallments.Count == 0)
            {
                continue;
            }

            var firstActive = eligibleInstallments[0];
            var installmentLabel = purchase.InstallmentCount > 1
                ? $"{purchase.InstallmentCount}x"
                : null;
            var displayDescription = installmentLabel is not null
                ? $"{purchase.Description} ({installmentLabel})"
                : purchase.Description;
            var statusLabel = firstActive.Cycle.GetStatus(today) == InvoiceStatus.Paid ? "Efetivado" : "Previsto";

            activities.Add(new DashboardRecentActivityDto(
                purchase.Id,
                displayDescription,
                purchase.Date,
                -purchase.Amount.Amount,
                "CardPurchase",
                "card",
                statusLabel,
                installmentLabel));
        }

        return activities
            .OrderByDescending(a => a.Date <= today)
            .ThenByDescending(a => a.Date)
            .ThenByDescending(a => Math.Abs(a.Amount))
            .ThenBy(a => a.Description, StringComparer.OrdinalIgnoreCase)
            .Take(MaxRecentActivities)
            .ToList();
    }

    private static string ResolveCategoryColor(Guid categoryId)
    {
        if (SystemCategoryStyles.TryGetValue(categoryId, out var style))
        {
            return style.HexColor;
        }

        var index = (int)((uint)categoryId.GetHashCode() % (uint)CustomCategoryPalette.Length);
        return CustomCategoryPalette[index];
    }

    private static string ResolveCategoryIcon(Guid categoryId) =>
        SystemCategoryStyles.TryGetValue(categoryId, out var style)
            ? style.IconKey
            : "tag";
}
