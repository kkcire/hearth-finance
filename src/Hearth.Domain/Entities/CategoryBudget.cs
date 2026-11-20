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
    public IReadOnlyList<AmountChange> AmountHistory => _amountHistory.AsReadOnly();

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
        ArgumentNullException.ThrowIfNull(startMonth);

        if (endMonth is not null && endMonth < startMonth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endMonth), endMonth, "The end month cannot be before the start month.");
        }

        DomainValidation.EnsurePositiveAmount(amount, nameof(amount), "budget amount");

        return new CategoryBudget(id, accountId, categoryId, startMonth, endMonth, amount);
    }

    /// <summary>
    /// The first month that can still be changed: the current calendar month.
    /// Past calendar months remain untouched to preserve historical targets.
    /// </summary>
    public static YearMonth FirstOpenMonth(DateOnly today) => YearMonth.From(today);

    public bool IsActiveIn(YearMonth month)
    {
        ArgumentNullException.ThrowIfNull(month);

        return month >= StartMonth && (EndMonth is null || month <= EndMonth);
    }

    /// <summary>
    /// True if this budget's active range shares at least one month with <c>[otherStart, otherEnd]</c>.
    /// </summary>
    public bool OverlapsWith(YearMonth otherStart, YearMonth? otherEnd)
    {
        ArgumentNullException.ThrowIfNull(otherStart);

        if (otherEnd is not null && otherEnd < otherStart)
        {
            throw new ArgumentOutOfRangeException(
                nameof(otherEnd), otherEnd, "The end month cannot be before the start month.");
        }

        return (EndMonth is null || otherStart <= EndMonth)
               && (otherEnd is null || StartMonth <= otherEnd);
    }

    public bool OverlapsWith(CategoryBudget other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return OverlapsWith(other.StartMonth, other.EndMonth);
    }

    /// <summary>The monthly target of the given month, or null if the budget is not active in it.</summary>
    public Money? AmountIn(YearMonth month)
    {
        if (!IsActiveIn(month))
        {
            return null;
        }

        for (var i = _amountHistory.Count - 1; i >= 0; i--)
        {
            if (_amountHistory[i].EffectiveFrom <= month)
            {
                return _amountHistory[i].Amount;
            }
        }

        throw new InvalidOperationException("The amount history does not cover the start month.");
    }

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

        var firstOpen = FirstOpenMonth(today);
        var effectiveFrom = firstOpen > StartMonth ? firstOpen : StartMonth;

        if (EndMonth is not null && effectiveFrom > EndMonth)
        {
            throw new DomainException(
                "The budget has already ended for every month that can still be changed.");
        }

        _amountHistory.RemoveAll(change => change.EffectiveFrom >= effectiveFrom);
        _amountHistory.Add(new AmountChange(effectiveFrom, newAmount));

        return effectiveFrom;
    }

    /// <summary>
    /// Sets the last active month. Only the current calendar month and later months can start
    /// or stop being active. Does nothing if the end month is already the given one.
    /// </summary>
    /// <exception cref="DomainException">The change would affect a month already in the past.</exception>
    public void End(YearMonth lastMonth, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(lastMonth);

        if (lastMonth < StartMonth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lastMonth), lastMonth, "The last month cannot be before the start month.");
        }

        if (EndMonth == lastMonth)
        {
            return;
        }

        var lastUnchangedMonth =
            EndMonth is { } currentEnd && currentEnd < lastMonth ? currentEnd : lastMonth;

        if (lastUnchangedMonth < YearMonth.Of(YearMonth.MaxYear, 12))
        {
            var firstChangedMonth = lastUnchangedMonth.AddMonths(1);

            if (firstChangedMonth < FirstOpenMonth(today))
            {
                throw new DomainException("Months that are already in the past cannot be changed.");
            }
        }

        EndMonth = lastMonth;
    }
}
