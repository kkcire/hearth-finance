using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Entities;

/// <summary>
/// Shared pure schedule logic for recurring monthly entities (<see cref="FixedExpense"/> and
/// <see cref="CategoryBudget"/>) that keep an inclusive <c>[StartMonth, EndMonth]</c> range and
/// an effective-dated amount history, without altering EF Core entity backing fields.
/// </summary>
internal static class MonthlyAmountSchedule
{
    public static YearMonth FirstOpenMonth(DateOnly today) => YearMonth.From(today);

    public static void ValidatePeriod(YearMonth startMonth, YearMonth? endMonth)
    {
        ArgumentNullException.ThrowIfNull(startMonth);

        if (endMonth is not null && endMonth < startMonth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endMonth), endMonth, "The end month cannot be before the start month.");
        }
    }

    public static bool IsActiveIn(YearMonth startMonth, YearMonth? endMonth, YearMonth month)
    {
        ArgumentNullException.ThrowIfNull(month);

        return month >= startMonth && (endMonth is null || month <= endMonth);
    }

    public static IReadOnlyList<AmountChange> OrderHistory(List<AmountChange> history) =>
        history.OrderBy(change => change.EffectiveFrom).ToList().AsReadOnly();

    public static Money? AmountIn(
        YearMonth startMonth,
        YearMonth? endMonth,
        List<AmountChange> history,
        YearMonth month)
    {
        if (!IsActiveIn(startMonth, endMonth, month))
        {
            return null;
        }

        AmountChange? latest = null;
        foreach (var change in history)
        {
            if (change.EffectiveFrom <= month
                && (latest is null || change.EffectiveFrom > latest.EffectiveFrom))
            {
                latest = change;
            }
        }

        return latest is not null
            ? latest.Amount
            : throw new InvalidOperationException("The amount history does not cover the start month.");
    }

    public static YearMonth ResolveEffectiveChangeMonth(
        YearMonth startMonth,
        YearMonth? endMonth,
        DateOnly today,
        string endedMessage)
    {
        var firstOpen = FirstOpenMonth(today);
        var effectiveFrom = firstOpen > startMonth ? firstOpen : startMonth;

        if (endMonth is not null && effectiveFrom > endMonth)
        {
            throw new DomainException(endedMessage);
        }

        return effectiveFrom;
    }

    public static void ApplyAmountChange(
        List<AmountChange> history,
        YearMonth effectiveFrom,
        Money newAmount)
    {
        history.RemoveAll(change => change.EffectiveFrom >= effectiveFrom);
        history.Add(new AmountChange(effectiveFrom, newAmount));
    }

    public static bool ValidateEndChange(
        YearMonth startMonth,
        YearMonth? currentEndMonth,
        YearMonth lastMonth,
        DateOnly today,
        string frozenMessage)
    {
        ArgumentNullException.ThrowIfNull(lastMonth);

        if (lastMonth < startMonth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lastMonth), lastMonth, "The last month cannot be before the start month.");
        }

        if (currentEndMonth == lastMonth)
        {
            return false;
        }

        var lastUnchangedMonth =
            currentEndMonth is { } currentEnd && currentEnd < lastMonth ? currentEnd : lastMonth;

        if (lastUnchangedMonth < YearMonth.Of(YearMonth.MaxYear, 12))
        {
            var firstChangedMonth = lastUnchangedMonth.AddMonths(1);

            if (firstChangedMonth < FirstOpenMonth(today))
            {
                throw new DomainException(frozenMessage);
            }
        }

        return true;
    }
}
