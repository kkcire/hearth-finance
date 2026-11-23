using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.ValueObjects;

public class CardBillingRuleTests
{
    // ---------- Creation ----------

    [Theory]
    [InlineData(1, 10)]
    [InlineData(3, 12)]
    [InlineData(15, 15)]
    [InlineData(25, 5)]
    [InlineData(28, 28)]
    public void Of_AcceptsClosingDayAndDueDayFromOneToTwentyEight(int closingDay, int dueDay)
    {
        var rule = CardBillingRule.Of(closingDay, dueDay);

        Assert.Equal(closingDay, rule.ClosingDay);
        Assert.Equal(dueDay, rule.DueDay);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(29)]
    [InlineData(31)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Of_RejectsClosingDayOutsideOneToTwentyEight_EvenWithValidDueDay(int closingDay)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CardBillingRule.Of(closingDay, 12));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(29)]
    [InlineData(31)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Of_RejectsDueDayOutsideOneToTwentyEight_EvenWithValidClosingDay(int dueDay)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CardBillingRule.Of(3, dueDay));
    }

    // ---------- Equality ----------

    [Fact]
    public void Equality_SameClosingDayAndDueDay_AreEqualWithSameHash()
    {
        var a = CardBillingRule.Of(3, 12);
        var b = CardBillingRule.Of(3, 12);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentClosingDayOrDueDay_AreNotEqual()
    {
        Assert.NotEqual(CardBillingRule.Of(3, 12), CardBillingRule.Of(4, 12));
        Assert.NotEqual(CardBillingRule.Of(3, 12), CardBillingRule.Of(3, 15));
        Assert.True(CardBillingRule.Of(3, 12) != CardBillingRule.Of(3, 15));
    }

    // ---------- Main invariant ----------

    [Fact]
    public void Of_AcceptsOneThroughTwentyEightForBothDays_AndRejectsValuesOutside()
    {
        for (var day = 1; day <= 28; day++)
        {
            var closingRule = CardBillingRule.Of(day, 10);
            var dueRule = CardBillingRule.Of(10, day);

            Assert.True(closingRule.ClosingDay == day, $"Closing day {day} was not preserved");
            Assert.True(dueRule.DueDay == day, $"Due day {day} was not preserved");
        }

        for (var day = -50; day <= 0; day++)
        {
            var invalid = day;
            Assert.Throws<ArgumentOutOfRangeException>(() => CardBillingRule.Of(invalid, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => CardBillingRule.Of(10, invalid));
        }

        for (var day = 29; day <= 60; day++)
        {
            var invalid = day;
            Assert.Throws<ArgumentOutOfRangeException>(() => CardBillingRule.Of(invalid, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => CardBillingRule.Of(10, invalid));
        }
    }

    [Fact]
    public void Of_BothDaysExistInEveryMonthOfALeapAndANonLeapYear()
    {
        for (var year = 2027; year <= 2028; year++)
        {
            for (var month = 1; month <= 12; month++)
            {
                var rule = CardBillingRule.Of(28, 28);

                Assert.Null(Record.Exception(() => new DateOnly(year, month, rule.ClosingDay)));
                Assert.Null(Record.Exception(() => new DateOnly(year, month, rule.DueDay)));
            }
        }
    }
}
