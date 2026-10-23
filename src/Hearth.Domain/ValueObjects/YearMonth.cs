namespace Hearth.Domain.ValueObjects;

/// <summary>
/// A calendar month of a specific year (e.g. 2026-10). Immutable and compared by value.
/// Always valid: it can only be created through <see cref="Of"/> or <see cref="From"/>.
/// </summary>
public sealed record YearMonth : IComparable<YearMonth>
{
    public const int MinYear = 1;
    public const int MaxYear = 9999;

    private const int MonthsPerYear = 12;

    private YearMonth(int year, int month)
    {
        Year = year;
        Month = month;
    }

    public int Year { get; }

    public int Month { get; }

    public DateOnly FirstDay => new(Year, Month, 1);

    public DateOnly LastDay => new(Year, Month, DateTime.DaysInMonth(Year, Month));

    public static YearMonth Of(int year, int month)
    {
        if (year < MinYear || year > MaxYear)
        {
            throw new ArgumentOutOfRangeException(
                nameof(year), year, $"The year must be between {MinYear} and {MaxYear}.");
        }

        if (month < 1 || month > MonthsPerYear)
        {
            throw new ArgumentOutOfRangeException(
                nameof(month), month, "The month must be between 1 and 12.");
        }

        return new YearMonth(year, month);
    }

    public static YearMonth From(DateOnly date) => new(date.Year, date.Month);

    /// <summary>
    /// Moves forward (or backward, with a negative value) by the given number of months,
    /// rolling over the year as needed.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The result falls outside the supported years.</exception>
    public YearMonth AddMonths(int months)
    {
        const long minIndex = (long)MinYear * MonthsPerYear;
        const long maxIndex = ((long)MaxYear * MonthsPerYear) + (MonthsPerYear - 1);

        var index = ((long)Year * MonthsPerYear) + (Month - 1) + months;

        if (index < minIndex || index > maxIndex)
        {
            throw new ArgumentOutOfRangeException(
                nameof(months), months, "The result is outside the supported range of years.");
        }

        return new YearMonth((int)(index / MonthsPerYear), (int)(index % MonthsPerYear) + 1);
    }

    public int CompareTo(YearMonth? other)
    {
        if (other is null)
        {
            return 1;
        }

        var byYear = Year.CompareTo(other.Year);
        return byYear != 0 ? byYear : Month.CompareTo(other.Month);
    }

    public static bool operator <(YearMonth? left, YearMonth? right) =>
        left is null ? right is not null : left.CompareTo(right) < 0;

    public static bool operator <=(YearMonth? left, YearMonth? right) =>
        left is null || left.CompareTo(right) <= 0;

    public static bool operator >(YearMonth? left, YearMonth? right) =>
        left is not null && left.CompareTo(right) > 0;

    public static bool operator >=(YearMonth? left, YearMonth? right) =>
        left is null ? right is null : left.CompareTo(right) >= 0;
}