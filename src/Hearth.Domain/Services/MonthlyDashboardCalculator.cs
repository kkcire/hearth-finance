using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Services;

/// <summary>
/// Pure domain calculator for the consolidated Monthly Dashboard hub.
/// Enforces strict cash accounting for account balance, isolated monthly results
/// (no cascading of unclosed future surpluses), break-even classification,
/// savings rate, future credit card installment totals, and isolated M + 1 estimation.
/// </summary>
public static class MonthlyDashboardCalculator
{
    public const int DefaultHistoryMonths = 6;
    public const int MaxHistoryMonths = 120;

    /// <summary>
    /// Attention band ratio above break-even (10% of monthly income).
    /// Results at R$ 0.00 or within this narrow margin classify as <see cref="BreakEvenStatus.BreakEvenWarning"/>.
    /// </summary>
    public const decimal BreakEvenWarningMarginRatio = 0.10m;

    public static MonthlyDashboardSnapshot Calculate(
        Account account,
        YearMonth selectedMonth,
        DateOnly asOfDate,
        DateOnly today,
        CreditCard? activeCard,
        IEnumerable<Income> incomes,
        IEnumerable<DirectExpense> directExpenses,
        IEnumerable<FixedExpense> fixedExpenses,
        IEnumerable<CreditCardPurchase> purchases,
        Guid? conditionId = null,
        int historyMonths = DefaultHistoryMonths,
        IEnumerable<FixedIncome>? fixedIncomes = null)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(selectedMonth);
        ArgumentNullException.ThrowIfNull(incomes);
        ArgumentNullException.ThrowIfNull(directExpenses);
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(purchases);
        DomainValidation.EnsureOptionalNotEmpty(conditionId, nameof(conditionId), "condition id");

        if (historyMonths < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(historyMonths), historyMonths, "History window must contain at least one month.");
        }

        if (asOfDate > today)
        {
            throw new DomainException("The balance cutoff date cannot be in the future.");
        }

        if (activeCard is not null)
        {
            if (activeCard.AccountId != account.Id)
            {
                throw new DomainException("The active credit card does not belong to this account.");
            }

            activeCard.EnsureActive();
        }

        var clampedHistoryMonths = Math.Min(historyMonths, MaxHistoryMonths);

        var accountIncomes = incomes.Where(i => i.AccountId == account.Id).ToList();
        var accountFixedIncomes = fixedIncomes is null
            ? []
            : fixedIncomes.Where(i => i.AccountId == account.Id).ToList();
        var accountDirect = directExpenses.Where(d => d.AccountId == account.Id).ToList();
        var accountFixed = fixedExpenses.Where(f => f.AccountId == account.Id).ToList();
        var accountPurchases = purchases.Where(p => p.AccountId == account.Id).ToList();

        // 1. Strict cash balance on cutoff date (never includes forecasts or future transactions)
        var balance = RealBalanceCalculator.Calculate(
            account,
            asOfDate,
            accountIncomes,
            accountDirect,
            accountFixed,
            accountPurchases,
            accountFixedIncomes);

        var currentCalendarMonth = YearMonth.From(today);

        // 2. Current invoice summary (advances sequentially to first unpaid cycle; preserves purchase snapshots)
        var currentInvoice = CalculateCurrentInvoiceSummary(
            account,
            selectedMonth,
            today,
            activeCard,
            accountPurchases,
            conditionId);

        // 3. Isolated current-month flow (no cascading from unclosed months)
        var currentFlow = CalculateIsolatedMonthFlow(
            account,
            selectedMonth,
            currentCalendarMonth,
            accountIncomes,
            accountDirect,
            accountFixed,
            accountPurchases,
            conditionId,
            accountFixedIncomes);

        // 4. Recent months isolated history ending at selectedMonth (clamped to account.TrackingStartPeriod)
        var history = BuildHistoryWindow(
            account,
            selectedMonth,
            currentCalendarMonth,
            clampedHistoryMonths,
            accountIncomes,
            accountDirect,
            accountFixed,
            accountPurchases,
            conditionId,
            accountFixedIncomes);

        // 5. Next-month estimate (M + 1; null at 9999-12). Carries projected cash balance only
        // when viewing the current calendar month without a condition filter.
        Money? realBalanceToday = selectedMonth == currentCalendarMonth && !conditionId.HasValue
            ? (asOfDate == today
                ? balance.Balance
                : RealBalanceCalculator.Calculate(
                    account,
                    today,
                    accountIncomes,
                    accountDirect,
                    accountFixed,
                    accountPurchases,
                    accountFixedIncomes).Balance)
            : null;

        var nextMonthEstimate = CalculateNextMonthEstimate(
            account,
            selectedMonth,
            today,
            accountIncomes,
            accountDirect,
            accountFixed,
            accountPurchases,
            conditionId,
            realBalanceToday,
            accountFixedIncomes);

        return new MonthlyDashboardSnapshot(
            balance,
            currentInvoice,
            currentFlow,
            history,
            nextMonthEstimate,
            conditionId);
    }

    public static decimal CalculateSavingsRate(Money monthlyIncome, Money totalExpenses) =>
        CalculateSavingsRate(monthlyIncome, totalExpenses, monthlyIncome);

    /// <summary>
    /// Calculates the personal savings rate against <paramref name="ownMonthlyIncome"/> so
    /// third-party reimbursements do not artificially dilute the denominator.
    /// Returns 0 when <paramref name="ownMonthlyIncome"/> is zero or negative, or when the
    /// net monthly surplus is zero or negative.
    /// </summary>
    public static decimal CalculateSavingsRate(
        Money monthlyIncome,
        Money totalExpenses,
        Money ownMonthlyIncome)
    {
        if (ownMonthlyIncome <= Money.Zero)
        {
            return 0m;
        }

        var surplus = monthlyIncome.Amount - totalExpenses.Amount;
        if (surplus <= 0m)
        {
            return 0m;
        }

        var rate = Math.Round((surplus / ownMonthlyIncome.Amount) * 100m, 1, MidpointRounding.AwayFromZero);
        return Math.Min(100m, rate);
    }

    public static BreakEvenStatus ClassifyBreakEven(
        Money monthlyIncome,
        Money totalExpenses)
    {
        if (monthlyIncome == Money.Zero && totalExpenses == Money.Zero)
        {
            return BreakEvenStatus.NoActivity;
        }

        var monthlyResult = monthlyIncome - totalExpenses;
        if (monthlyResult < Money.Zero)
        {
            return BreakEvenStatus.Deficit;
        }

        if (monthlyResult == Money.Zero)
        {
            return BreakEvenStatus.BreakEvenWarning;
        }

        var warningCeiling = Math.Round(
            monthlyIncome.Amount * BreakEvenWarningMarginRatio,
            2,
            MidpointRounding.AwayFromZero);

        return monthlyResult.Amount <= warningCeiling
            ? BreakEvenStatus.BreakEvenWarning
            : BreakEvenStatus.Healthy;
    }

    public static CurrentInvoiceSummary CalculateCurrentInvoiceSummary(
        Account account,
        YearMonth selectedMonth,
        DateOnly today,
        CreditCard? activeCard,
        IReadOnlyCollection<CreditCardPurchase> accountPurchases,
        Guid? conditionId)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(selectedMonth);
        ArgumentNullException.ThrowIfNull(accountPurchases);
        DomainValidation.EnsureOptionalNotEmpty(conditionId, nameof(conditionId), "condition id");

        var currentCalendarMonth = YearMonth.From(today);
        var effectiveBillingMonth = selectedMonth;
        BillingCycle? fallbackCardCycle = null;

        if (activeCard is not null && TryResolveDueCycle(selectedMonth, activeCard.BillingRule, out var cardCycle))
        {
            // When viewing the current calendar month and its invoice due date has already passed (paid),
            // advance sequentially cycle-by-cycle to the first unpaid cycle so closed-and-unpaid invoices
            // (common when DueDay <= ClosingDay) are NEVER skipped.
            if (selectedMonth == currentCalendarMonth)
            {
                while (cardCycle.DueDate <= today && TryAdvanceCycle(cardCycle, out var nextCycle))
                {
                    cardCycle = nextCycle;
                }

                effectiveBillingMonth = cardCycle.DueMonth;
            }

            fallbackCardCycle = cardCycle;
        }

        // Preserve each purchase's billing cycle snapshot deterministically when installments exist
        // in the effective due month; fall back to the active card's rule only when empty.
        var snapshotCycle = accountPurchases
            .Where(p => p.AccountId == account.Id
                        && (!conditionId.HasValue || p.ConditionId == conditionId.Value))
            .SelectMany(p => p.GetTrackedInstallments(account.TrackingStartDate))
            .Where(i => i.Cycle.DueMonth == effectiveBillingMonth)
            .Select(i => i.Cycle)
            .OrderBy(c => c.DueDate)
            .ThenBy(c => c.ClosingDate)
            .FirstOrDefault();

        var resolvedCycle = snapshotCycle ?? fallbackCardCycle;

        var cycleAmount = InvoiceCalculator.CalculateByDueMonth(
            account.Id, effectiveBillingMonth, accountPurchases, conditionId, account.TrackingStartDate);
        var futureTotal = SumEligibleInstallments(
            account, accountPurchases, conditionId, cycle => cycle.DueMonth > effectiveBillingMonth);

        return new CurrentInvoiceSummary(
            effectiveBillingMonth,
            cycleAmount,
            resolvedCycle?.DueDate,
            resolvedCycle?.ClosingDate,
            resolvedCycle?.GetStatus(today),
            futureTotal,
            activeCard is not null);
    }

    public static MonthlyFlowSummary CalculateIsolatedMonthFlow(
        Account account,
        YearMonth month,
        YearMonth currentCalendarMonth,
        IReadOnlyCollection<Income> accountIncomes,
        IReadOnlyCollection<DirectExpense> accountDirect,
        IReadOnlyCollection<FixedExpense> accountFixed,
        IReadOnlyCollection<CreditCardPurchase> accountPurchases,
        Guid? conditionId,
        IReadOnlyCollection<FixedIncome>? accountFixedIncomes = null)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(month);
        ArgumentNullException.ThrowIfNull(accountIncomes);
        ArgumentNullException.ThrowIfNull(accountDirect);
        ArgumentNullException.ThrowIfNull(accountFixed);
        ArgumentNullException.ThrowIfNull(accountPurchases);

        var isProjection = month >= currentCalendarMonth;

        // Periods strictly prior to TrackingStartPeriod are outside tracked history.
        if (!account.CanRegisterInMonth(month))
        {
            return new MonthlyFlowSummary(
                month,
                Money.Zero,
                Money.Zero,
                Money.Zero,
                Money.Zero,
                Money.Zero,
                Money.Zero,
                Money.Zero,
                0m,
                BreakEvenStatus.NoActivity,
                Money.Zero,
                isProjection);
        }

        var eligibleIncomes = accountIncomes
            .Where(income => income.AccountId == account.Id
                             && income.Month == month
                             && account.CanRegisterOn(income.Date)
                             && (!conditionId.HasValue || income.ConditionId == conditionId.Value))
            .ToList();

        var (fixedIncomeTotal, ownFixedIncomeTotal) = SumEffectiveFixedIncomesForMonth(
            account, month, accountFixedIncomes, conditionId);

        var monthlyIncome = Money.Sum(eligibleIncomes.Select(income => income.Amount)) + fixedIncomeTotal;
        var ownMonthlyIncome = Money.Sum(eligibleIncomes.Where(income => income.IsOwn).Select(income => income.Amount))
            + ownFixedIncomeTotal;

        var directTotal = Money.Sum(
            accountDirect
                .Where(expense => expense.AccountId == account.Id
                                  && expense.Month == month
                                  && account.CanRegisterOn(expense.Date))
                .Select(expense => expense.Amount));

        var fixedTotal = Money.Sum(
            accountFixed
                .Where(expense => expense.AccountId == account.Id)
                .Select(expense => expense.AmountIn(month))
                .Where(amount => amount.HasValue)
                .Select(amount => amount!.Value));

        var cardInvoice = InvoiceCalculator.CalculateByDueMonth(
            account.Id, month, accountPurchases, conditionId, account.TrackingStartDate);

        var totalExpenses = directTotal + fixedTotal + cardInvoice;
        var result = monthlyIncome - totalExpenses;
        var committedFloor = fixedTotal + cardInvoice;
        var savingsRate = CalculateSavingsRate(monthlyIncome, totalExpenses, ownMonthlyIncome);
        var status = ClassifyBreakEven(monthlyIncome, totalExpenses);

        return new MonthlyFlowSummary(
            month,
            monthlyIncome,
            ownMonthlyIncome,
            directTotal,
            fixedTotal,
            cardInvoice,
            totalExpenses,
            result,
            savingsRate,
            status,
            committedFloor,
            isProjection);
    }

    public static NextMonthEstimate? CalculateNextMonthEstimate(
        Account account,
        YearMonth selectedMonth,
        DateOnly today,
        IReadOnlyCollection<Income> accountIncomes,
        IReadOnlyCollection<DirectExpense> accountDirect,
        IReadOnlyCollection<FixedExpense> accountFixed,
        IReadOnlyCollection<CreditCardPurchase> accountPurchases,
        Guid? conditionId,
        Money? realBalanceToday = null,
        IReadOnlyCollection<FixedIncome>? accountFixedIncomes = null)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(selectedMonth);
        ArgumentNullException.ThrowIfNull(accountIncomes);
        ArgumentNullException.ThrowIfNull(accountDirect);
        ArgumentNullException.ThrowIfNull(accountFixed);
        ArgumentNullException.ThrowIfNull(accountPurchases);

        if (selectedMonth.Year == YearMonth.MaxYear && selectedMonth.Month == 12)
        {
            return null;
        }

        var nextMonth = selectedMonth.AddMonths(1);

        var nextMonthIncomes = account.CanRegisterInMonth(nextMonth)
            ? accountIncomes
                .Where(income => income.AccountId == account.Id
                                 && income.Month == nextMonth
                                 && account.CanRegisterOn(income.Date)
                                 && (!conditionId.HasValue || income.ConditionId == conditionId.Value))
                .ToList()
            : [];

        var (nextMonthFixedIncomeTotal, nextMonthOwnFixedIncomeTotal) = account.CanRegisterInMonth(nextMonth)
            ? SumEffectiveFixedIncomesForMonth(account, nextMonth, accountFixedIncomes, conditionId)
            : (Money.Zero, Money.Zero);

        var estimatedOneOffIncome = Money.Sum(nextMonthIncomes.Select(income => income.Amount));
        var estimatedIncome = estimatedOneOffIncome + nextMonthFixedIncomeTotal;
        var estimatedOwnIncome = Money.Sum(nextMonthIncomes.Where(income => income.IsOwn).Select(income => income.Amount))
            + nextMonthOwnFixedIncomeTotal;

        var estimatedFixed = account.CanRegisterInMonth(nextMonth)
            ? Money.Sum(
                accountFixed
                    .Where(expense => expense.AccountId == account.Id)
                    .Select(expense => expense.AmountIn(nextMonth))
                    .Where(amount => amount.HasValue)
                    .Select(amount => amount!.Value))
            : Money.Zero;

        var estimatedInstallments = InvoiceCalculator.CalculateByDueMonth(
            account.Id, nextMonth, accountPurchases, conditionId, account.TrackingStartDate);

        var estimatedDirect = Money.Sum(
            accountDirect
                .Where(expense => expense.AccountId == account.Id
                                  && expense.Month == nextMonth
                                  && account.CanRegisterOn(expense.Date))
                .Select(expense => expense.Amount));

        var estimatedCommitments = estimatedFixed + estimatedInstallments + estimatedDirect;
        var projectedResult = estimatedIncome - estimatedCommitments;
        var estimatedSavingsRate = CalculateSavingsRate(estimatedIncome, estimatedCommitments, estimatedOwnIncome);

        Money? projectedOpeningBalance = null;
        Money? projectedEndingBalance = null;

        var currentCalendarMonth = YearMonth.From(today);
        if (selectedMonth == currentCalendarMonth && !conditionId.HasValue)
        {
            var cashToday = realBalanceToday ?? RealBalanceCalculator.Calculate(
                account,
                today,
                accountIncomes,
                accountDirect,
                accountFixed,
                accountPurchases,
                accountFixedIncomes).Balance;

            var pendingCurrentMonthNet = CalculateUnsettledCurrentMonthNet(
                account,
                currentCalendarMonth,
                today,
                accountIncomes,
                accountDirect,
                accountFixed,
                accountPurchases,
                accountFixedIncomes);

            projectedOpeningBalance = cashToday + pendingCurrentMonthNet;

            var unsettledNextFixedIncomes = account.CanRegisterInMonth(nextMonth)
                ? SumUnsettledFixedIncomesForMonth(account, nextMonth, today, accountFixedIncomes)
                : Money.Zero;

            var unsettledNextFixed = account.CanRegisterInMonth(nextMonth)
                ? Money.Sum(
                    accountFixed
                        .Where(expense => expense.AccountId == account.Id
                                          && !IsSettledByDate(account, expense.PaidOn(nextMonth), today))
                        .Select(expense => expense.AmountIn(nextMonth))
                        .Where(amount => amount.HasValue)
                        .Select(amount => amount!.Value))
                : Money.Zero;

            projectedEndingBalance = projectedOpeningBalance.Value
                + estimatedOneOffIncome
                + unsettledNextFixedIncomes
                - unsettledNextFixed
                - estimatedInstallments
                - estimatedDirect;
        }

        return new NextMonthEstimate(
            nextMonth,
            estimatedIncome,
            estimatedFixed,
            estimatedInstallments,
            estimatedDirect,
            estimatedCommitments,
            projectedResult,
            estimatedSavingsRate,
            projectedOpeningBalance,
            projectedEndingBalance);
    }

    private static Money CalculateUnsettledCurrentMonthNet(
        Account account,
        YearMonth currentMonth,
        DateOnly today,
        IReadOnlyCollection<Income> accountIncomes,
        IReadOnlyCollection<DirectExpense> accountDirect,
        IReadOnlyCollection<FixedExpense> accountFixed,
        IReadOnlyCollection<CreditCardPurchase> accountPurchases,
        IReadOnlyCollection<FixedIncome>? accountFixedIncomes)
    {
        if (!account.CanRegisterInMonth(currentMonth))
        {
            return Money.Zero;
        }

        var pendingOneOffIncomes = Money.Sum(
            accountIncomes
                .Where(income => income.AccountId == account.Id
                                 && income.Month == currentMonth
                                 && account.CanRegisterOn(income.Date)
                                 && !income.IsReceivedOn(today))
                .Select(income => income.Amount));

        var pendingFixedIncomes = SumUnsettledFixedIncomesForMonth(
            account, currentMonth, today, accountFixedIncomes);

        var pendingIncomes = pendingOneOffIncomes + pendingFixedIncomes;

        var pendingDirect = Money.Sum(
            accountDirect
                .Where(expense => expense.AccountId == account.Id
                                  && expense.Month == currentMonth
                                  && account.CanRegisterOn(expense.Date)
                                  && expense.Date > today)
                .Select(expense => expense.Amount));

        var pendingFixed = Money.Sum(
            accountFixed
                .Where(expense => expense.AccountId == account.Id
                                  && !IsSettledByDate(account, expense.PaidOn(currentMonth), today))
                .Select(expense => expense.AmountIn(currentMonth))
                .Where(amount => amount.HasValue)
                .Select(amount => amount!.Value));

        var pendingCard = SumEligibleInstallments(
            account,
            accountPurchases,
            conditionId: null,
            cycle => cycle.DueMonth == currentMonth && cycle.DueDate > today);

        return pendingIncomes - pendingDirect - pendingFixed - pendingCard;
    }

    private static (Money Total, Money OwnTotal) SumEffectiveFixedIncomesForMonth(
        Account account,
        YearMonth month,
        IReadOnlyCollection<FixedIncome>? accountFixedIncomes,
        Guid? conditionId)
    {
        if (accountFixedIncomes is null || accountFixedIncomes.Count == 0)
        {
            return (Money.Zero, Money.Zero);
        }

        var total = Money.Zero;
        var ownTotal = Money.Zero;

        foreach (var fixedIncome in accountFixedIncomes)
        {
            if (fixedIncome.AccountId != account.Id
                || (conditionId.HasValue && fixedIncome.ConditionId != conditionId.Value))
            {
                continue;
            }

            var receipt = fixedIncome.GetReceiptFor(month);
            Money amount;
            if (receipt is not null)
            {
                amount = receipt.ActualAmount;
            }
            else if (fixedIncome.IsActiveIn(month))
            {
                amount = fixedIncome.GetExpectedAmountFor(month);
            }
            else
            {
                continue;
            }

            total += amount;
            if (fixedIncome.IsOwn)
            {
                ownTotal += amount;
            }
        }

        return (total, ownTotal);
    }

    private static Money SumUnsettledFixedIncomesForMonth(
        Account account,
        YearMonth month,
        DateOnly today,
        IReadOnlyCollection<FixedIncome>? accountFixedIncomes)
    {
        if (accountFixedIncomes is null || accountFixedIncomes.Count == 0)
        {
            return Money.Zero;
        }

        var total = Money.Zero;
        foreach (var fixedIncome in accountFixedIncomes)
        {
            if (fixedIncome.AccountId != account.Id)
            {
                continue;
            }

            var receipt = fixedIncome.GetReceiptFor(month);
            if (receipt is not null)
            {
                if (!IsSettledByDate(account, receipt.ReceivedOn, today))
                {
                    total += receipt.ActualAmount;
                }
            }
            else if (fixedIncome.IsActiveIn(month))
            {
                total += fixedIncome.GetExpectedAmountFor(month);
            }
        }

        return total;
    }

    private static bool IsSettledByDate(Account account, DateOnly? settledOn, DateOnly cutoff) =>
        settledOn is { } date && account.CanRegisterOn(date) && date <= cutoff;

    private static IReadOnlyList<MonthlyFlowSummary> BuildHistoryWindow(
        Account account,
        YearMonth selectedMonth,
        YearMonth currentCalendarMonth,
        int historyMonths,
        IReadOnlyCollection<Income> accountIncomes,
        IReadOnlyCollection<DirectExpense> accountDirect,
        IReadOnlyCollection<FixedExpense> accountFixed,
        IReadOnlyCollection<CreditCardPurchase> accountPurchases,
        Guid? conditionId,
        IReadOnlyCollection<FixedIncome>? accountFixedIncomes)
    {
        var initialMonth = account.TrackingStartPeriod;
        if (selectedMonth < initialMonth)
        {
            return [];
        }

        var startMonth = DetermineHistoryStart(selectedMonth, initialMonth, historyMonths);
        var monthSpan = ((selectedMonth.Year - startMonth.Year) * 12) + (selectedMonth.Month - startMonth.Month) + 1;
        var items = new List<MonthlyFlowSummary>(monthSpan);

        var month = startMonth;
        while (true)
        {
            items.Add(CalculateIsolatedMonthFlow(
                account,
                month,
                currentCalendarMonth,
                accountIncomes,
                accountDirect,
                accountFixed,
                accountPurchases,
                conditionId,
                accountFixedIncomes));

            if (month == selectedMonth)
            {
                break;
            }

            month = month.AddMonths(1);
        }

        return items.AsReadOnly();
    }

    private static YearMonth DetermineHistoryStart(YearMonth selectedMonth, YearMonth initialMonth, int historyMonths)
    {
        var desired = ClampAddMonths(selectedMonth, -(historyMonths - 1));
        return desired < initialMonth ? initialMonth : desired;
    }

    private static YearMonth ClampAddMonths(YearMonth origin, int delta)
    {
        const long minIndex = (long)YearMonth.MinYear * 12;
        const long maxIndex = ((long)YearMonth.MaxYear * 12) + 11;

        var targetIndex = Math.Clamp(((long)origin.Year * 12) + (origin.Month - 1) + delta, minIndex, maxIndex);
        return YearMonth.Of((int)(targetIndex / 12), (int)(targetIndex % 12) + 1);
    }

    private static bool TryResolveDueCycle(YearMonth dueMonth, CardBillingRule rule, out BillingCycle cycle)
    {
        try
        {
            cycle = BillingCycle.DueIn(dueMonth, rule);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            cycle = null!;
            return false;
        }
    }

    private static bool TryAdvanceCycle(BillingCycle current, out BillingCycle next)
    {
        try
        {
            next = current.AddCycles(1);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            next = null!;
            return false;
        }
    }

    private static Money SumEligibleInstallments(
        Account account,
        IReadOnlyCollection<CreditCardPurchase> accountPurchases,
        Guid? conditionId,
        Func<BillingCycle, bool> cyclePredicate) =>
        Money.Sum(
            accountPurchases
                .Where(purchase => purchase.AccountId == account.Id
                                   && (!conditionId.HasValue || purchase.ConditionId == conditionId.Value))
                .SelectMany(purchase => purchase.GetTrackedInstallments(account.TrackingStartDate))
                .Where(installment => cyclePredicate(installment.Cycle))
                .Select(installment => installment.Amount));
}
