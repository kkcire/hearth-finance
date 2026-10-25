using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.ValueObjects;

public class YearMonthTests
{
    // ---------- Creation ----------

    [Theory]
    [InlineData(2026, 1)]
    [InlineData(2026, 12)]
    [InlineData(1, 1)]
    [InlineData(9999, 12)]
    public void Of_AcceptsValidYearAndMonth(int year, int month)
    {
        var yearMonth = YearMonth.Of(year, month);

        Assert.Equal(year, yearMonth.Year);
        Assert.Equal(month, yearMonth.Month);
    }

    [Theory]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    [InlineData(2026, -1)]
    public void Of_RejectsInvalidMonth(int year, int month)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => YearMonth.Of(year, month));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(10000, 1)]
    [InlineData(-1, 1)]
    public void Of_RejectsInvalidYear(int year, int month)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => YearMonth.Of(year, month));
    }

    // ---------- From(DateOnly) ----------

    [Theory]
    [InlineData(2026, 10, 1)]
    [InlineData(2026, 10, 31)]
    [InlineData(2026, 2, 28)]
    [InlineData(2028, 2, 29)]
    [InlineData(2026, 12, 31)]
    public void From_TakesYearAndMonthOfTheDate(int year, int month, int day)
    {
        var yearMonth = YearMonth.From(new DateOnly(year, month, day));

        Assert.Equal(year, yearMonth.Year);
        Assert.Equal(month, yearMonth.Month);
    }

    // ---------- Equality ----------

    [Fact]
    public void Equality_SameYearAndMonth_AreEqualWithSameHash()
    {
        var a = YearMonth.Of(2026, 10);
        var b = YearMonth.Of(2026, 10);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentMonthOrYear_AreNotEqual()
    {
        Assert.NotEqual(YearMonth.Of(2026, 10), YearMonth.Of(2026, 11));
        Assert.NotEqual(YearMonth.Of(2026, 10), YearMonth.Of(2027, 10));
    }

    [Fact]
    public void Equality_FromDateAndOf_ProduceEqualValues()
    {
        Assert.Equal(YearMonth.Of(2026, 10), YearMonth.From(new DateOnly(2026, 10, 22)));
    }

    // ---------- AddMonths ----------

    [Theory]
    [InlineData(2026, 12, 1, 2027, 1)]
    [InlineData(2026, 1, -1, 2025, 12)]
    [InlineData(2026, 10, 12, 2027, 10)]
    [InlineData(2026, 10, -12, 2025, 10)]
    [InlineData(2026, 10, 0, 2026, 10)]
    [InlineData(2026, 10, 25, 2028, 11)]
    [InlineData(2026, 3, -14, 2025, 1)]
    [InlineData(9999, 11, 1, 9999, 12)]
    [InlineData(1, 2, -1, 1, 1)]
    public void AddMonths_RollsOverTheYearCorrectly(
        int year, int month, int months, int expectedYear, int expectedMonth)
    {
        var result = YearMonth.Of(year, month).AddMonths(months);

        Assert.Equal(YearMonth.Of(expectedYear, expectedMonth), result);
    }

    [Theory]
    [InlineData(9999, 12, 1)]
    [InlineData(9999, 1, 12)]
    [InlineData(1, 1, -1)]
    [InlineData(1, 12, -12)]
    [InlineData(2026, 10, int.MaxValue)]
    [InlineData(2026, 10, int.MinValue)]
    public void AddMonths_OutsideSupportedYears_Throws(int year, int month, int months)
    {
        var yearMonth = YearMonth.Of(year, month);

        Assert.Throws<ArgumentOutOfRangeException>(() => yearMonth.AddMonths(months));
    }

    // ---------- FirstDay and LastDay ----------

    [Theory]
    [InlineData(2026, 2, 28)]
    [InlineData(2028, 2, 29)]
    [InlineData(2000, 2, 29)]
    [InlineData(2100, 2, 28)]
    [InlineData(2026, 4, 30)]
    [InlineData(2026, 10, 31)]
    public void FirstDayAndLastDay_AreCorrectForEachMonthLength(int year, int month, int lastDay)
    {
        var yearMonth = YearMonth.Of(year, month);

        Assert.Equal(new DateOnly(year, month, 1), yearMonth.FirstDay);
        Assert.Equal(new DateOnly(year, month, lastDay), yearMonth.LastDay);
    }

    // ---------- Comparison ----------

    [Fact]
    public void Comparison_SameYear_ComparesByMonth()
    {
        var earlier = YearMonth.Of(2026, 1);
        var later = YearMonth.Of(2026, 2);

        Assert.True(earlier < later);
        Assert.True(later > earlier);
        Assert.True(earlier <= later);
        Assert.True(later >= earlier);
        Assert.False(earlier > later);
        Assert.False(later < earlier);
    }

    [Fact]
    public void Comparison_DifferentYears_YearWinsOverMonth()
    {
        var earlier = YearMonth.Of(2025, 12);
        var later = YearMonth.Of(2026, 1);

        Assert.True(earlier < later);
        Assert.True(later > earlier);
        Assert.True(earlier.CompareTo(later) < 0);
        Assert.True(later.CompareTo(earlier) > 0);
    }

    [Fact]
    public void Comparison_EqualValues()
    {
        var a = YearMonth.Of(2026, 10);
        var b = YearMonth.Of(2026, 10);

        Assert.Equal(0, a.CompareTo(b));
        Assert.True(a <= b);
        Assert.True(a >= b);
        Assert.False(a < b);
        Assert.False(a > b);
    }

    [Fact]
    public void Comparison_Null_IsSmallerThanAnyValue()
    {
        YearMonth? none = null;
        var value = YearMonth.Of(2026, 10);

        Assert.True(value.CompareTo(null) > 0);
        Assert.True(none < value);
        Assert.True(none <= value);
        Assert.True(value > none);
        Assert.True(value >= none);
        Assert.False(none > value);
        Assert.False(value < none);
    }

    // ---------- Main invariants ----------

    [Fact]
    public void AddMonths_OneAtATime_WalksEveryMonthOfTheSupportedRange()
    {
        var current = YearMonth.Of(YearMonth.MinYear, 1);

        for (var year = YearMonth.MinYear; year <= YearMonth.MaxYear; year++)
        {
            for (var month = 1; month <= 12; month++)
            {
                Assert.True(
                    current == YearMonth.Of(year, month),
                    $"Expected {year}-{month:00} but got {current.Year}-{current.Month:00}");

                if (year == YearMonth.MaxYear && month == 12)
                {
                    break;
                }

                var next = current.AddMonths(1);
                Assert.True(next > current, $"AddMonths(1) did not advance {year}-{month:00}");
                current = next;
            }
        }
    }

    [Theory]
    [InlineData(2026, 1)]
    [InlineData(2026, 10)]
    [InlineData(2026, 12)]
    [InlineData(2028, 2)]
    [InlineData(1990, 6)]
    public void AddMonths_ThenNegative_ReturnsToTheStart(int year, int month)
    {
        var start = YearMonth.Of(year, month);

        for (var n = -600; n <= 600; n++)
        {
            Assert.True(
                start.AddMonths(n).AddMonths(-n) == start,
                $"Round trip failed for {year}-{month:00} with n={n}");
        }
    }

    [Fact]
    public void FromFirstDayAndFromLastDay_ReturnTheOriginalYearMonth_ForEveryMonth()
    {
        for (var year = YearMonth.MinYear; year <= YearMonth.MaxYear; year++)
        {
            for (var month = 1; month <= 12; month++)
            {
                var yearMonth = YearMonth.Of(year, month);

                Assert.True(
                    YearMonth.From(yearMonth.FirstDay) == yearMonth,
                    $"FirstDay round trip failed for {year}-{month:00}");
                Assert.True(
                    YearMonth.From(yearMonth.LastDay) == yearMonth,
                    $"LastDay round trip failed for {year}-{month:00}");
            }
        }
    }
}