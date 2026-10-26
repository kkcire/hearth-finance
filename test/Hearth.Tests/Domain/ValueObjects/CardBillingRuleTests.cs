using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.ValueObjects;

public class CardBillingRuleTests
{
    // ---------- Creation ----------

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(15)]
    [InlineData(28)]
    public void Of_AcceptsClosingDayFromOneToTwentyEight(int closingDay)
    {
        var rule = CardBillingRule.Of(closingDay, closingDay);

        Assert.Equal(closingDay, rule.ClosingDay);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(29)]
    [InlineData(31)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Of_RejectsClosingDayOutsideOneToTwentyEight(int closingDay)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CardBillingRule.Of(closingDay, closingDay));
    }

    [Fact]
    public void Of_ExposesTheLimitsAsConstants()
    {
        Assert.Equal(1, CardBillingRule.MinClosingDay);
        Assert.Equal(28, CardBillingRule.MaxClosingDay);
    }

    // ---------- Equality ----------

    [Fact]
    public void Equality_SameClosingDay_AreEqualWithSameHash()
    {
        var a = CardBillingRule.Of(3, 3);
        var b = CardBillingRule.Of(3, 3);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentClosingDay_AreNotEqual()
    {
        Assert.NotEqual(CardBillingRule.Of(3, 3), CardBillingRule.Of(4, 4));
        Assert.True(CardBillingRule.Of(3, 3) != CardBillingRule.Of(4, 4));
    }

    // ---------- Main invariant ----------

    [Fact]
    public void Of_AcceptsEveryDayInTheRange_AndRejectsEveryDayAroundIt()
    {
        for (var day = CardBillingRule.MinClosingDay; day <= CardBillingRule.MaxClosingDay; day++)
        {
            var rule = CardBillingRule.Of(day, day);

            Assert.True(rule.ClosingDay == day, $"Closing day {day} was not preserved");
        }

        for (var day = -100; day < CardBillingRule.MinClosingDay; day++)
        {
            var invalid = day;
            Assert.Throws<ArgumentOutOfRangeException>(() => CardBillingRule.Of(invalid, invalid));
        }

        for (var day = CardBillingRule.MaxClosingDay + 1; day <= 100; day++)
        {
            var invalid = day;
            Assert.Throws<ArgumentOutOfRangeException>(() => CardBillingRule.Of(invalid, invalid));
        }
    }

    [Fact]
    public void Of_ClosingDayExistsInEveryMonthOfALeapAndANonLeapYear()
    {
        // The reason for the 28 limit: the closing day must be a real date in every month.
        for (var year = 2027; year <= 2028; year++)
        {
            for (var month = 1; month <= 12; month++)
            {
                var rule = CardBillingRule.Of(CardBillingRule.MaxClosingDay, CardBillingRule.MaxClosingDay);

                var exception = Record.Exception(() => new DateOnly(year, month, rule.ClosingDay));

                Assert.Null(exception);
            }
        }
    }
}