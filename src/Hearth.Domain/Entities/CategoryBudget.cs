using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Entities;

/// <summary>
/// A monthly spending target for one category of an account, active from a start month until
/// an optional end month. Past calendar months are preserved: amount changes keep a history
/// and only apply from the current calendar month onward. Budgets are purely informational and
/// never block expenses.
/// </summary>
public sealed class CategoryBudget
{
    private readonly List<AmountChange> _amountHistory;

    private CategoryBudget()
    {
        StartMonth = null!;
        _amountHistory = [];
    }

    private CategoryBudget(
        Guid id,
        Guid accountId,
        Guid categoryId,
        YearMonth startMonth,
        YearMonth? endMonth,
        Money amount)
    {
        Id = id;
        AccountId = accountId;
        CategoryId = categoryId;
        StartMonth = startMonth;
        EndMonth = endMonth;
        _amountHistory = [new AmountChange(startMonth, amount)];
    }

    public Guid Id { get; }

    public Guid AccountId { get; }

    public Guid CategoryId { get; }

    /// <summary>The first month in which the budget is active.</summary>
    public YearMonth StartMonth { get; }

    /// <summary>The last month in which the budget is active (inclusive). Null while it has no end.</summary>
    public YearMonth? EndMonth { get; private set; }

    /// <summary>Monthly limits in order of effective month. The first entry always starts at <see cref="StartMonth"/>.</summary>
    public IReadOnlyList<AmountChange> AmountHistory =>
        MonthlyAmountSchedule.OrderHistory(_amountHistory);

    public static CategoryBudget Create(
        Guid id,
        Guid accountId,
        Guid categoryId,
        YearMonth startMonth,
        YearMonth? endMonth,
        Money amount)
    {
        DomainValidation.EnsureNotEmpty(id, nameof(id));
        DomainValidation.EnsureNotEmpty(accountId, nameof(accountId), "account id");
        DomainValidation.EnsureNotEmpty(categoryId, nameof(categoryId), "category id");
        MonthlyAmountSchedule.ValidatePeriod(startMonth, endMonth);
        DomainValidation.EnsurePositiveAmount(amount, nameof(amount), "budget amount");

        return new CategoryBudget(id, accountId, categoryId, startMonth, endMonth, amount);
    }

    /// <summary>
    /// The first month that can still be changed: the current calendar month.
    /// Past calendar months remain untouched to preserve historical targets.
    /// </summary>
    public static YearMonth FirstOpenMonth(DateOnly today) => MonthlyAmountSchedule.FirstOpenMonth(today);

    public bool IsActiveIn(YearMonth month) =>
        MonthlyAmountSchedule.IsActiveIn(StartMonth, EndMonth, month);

    /// <summary>
    /// True if this budget's active range shares at least one month with <c>[otherStart, otherEnd]</c>.
    /// </summary>
    public bool OverlapsWith(YearMonth otherStart, YearMonth? otherEnd)
    {
        MonthlyAmountSchedule.ValidatePeriod(otherStart, otherEnd);

        return (EndMonth is null || otherStart <= EndMonth)
               && (otherEnd is null || StartMonth <= otherEnd);
    }

    public bool OverlapsWith(CategoryBudget other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return OverlapsWith(other.StartMonth, other.EndMonth);
    }

    /// <summary>The monthly target of the given month, or null if the budget is not active in it.</summary>
    public Money? AmountIn(YearMonth month) =>
        MonthlyAmountSchedule.AmountIn(StartMonth, EndMonth, _amountHistory, month);

    /// <summary>
    /// Changes the monthly limit from the current calendar month (or <see cref="StartMonth"/> if
    /// later) onward, keeping the history of earlier months. A later edit replaces any change
    /// already scheduled from that month on.
    /// </summary>
    /// <returns>The month from which the new limit applies.</returns>
    /// <exception cref="DomainException">The budget has already ended for every month that can still be changed.</exception>
    public YearMonth ChangeAmount(Money newAmount, DateOnly today)
    {
        DomainValidation.EnsurePositiveAmount(newAmount, nameof(newAmount), "budget amount");

        var effectiveFrom = MonthlyAmountSchedule.ResolveEffectiveChangeMonth(
            StartMonth,
            EndMonth,
            today,
            "The budget has already ended for every month that can still be changed.");

        MonthlyAmountSchedule.ApplyAmountChange(_amountHistory, effectiveFrom, newAmount);
        return effectiveFrom;
    }

    /// <summary>
    /// Sets the last active month. Only the current calendar month and later months can start
    /// or stop being active. Does nothing if the end month is already the given one.
    /// </summary>
    /// <exception cref="DomainException">The change would affect a month already in the past.</exception>
    public void End(YearMonth lastMonth, DateOnly today)
    {
        var hasChange = MonthlyAmountSchedule.ValidateEndChange(
            StartMonth,
            EndMonth,
            lastMonth,
            today,
            "Months that are already in the past cannot be changed.");

        if (!hasChange)
        {
            return;
        }

        EndMonth = lastMonth;
    }
}
