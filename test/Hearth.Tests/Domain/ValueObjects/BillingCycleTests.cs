using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.ValueObjects;

public class BillingCycleTests
{
    private static readonly CardBillingRule Rule3 = CardBillingRule.Of(3, 3);

    private static BillingCycle Cycle(int year, int month, CardBillingRule? rule = null) =>
        BillingCycle.Closing(YearMonth.Of(year, month), rule ?? Rule3);

    // ---------- ForPurchase: which cycle a purchase belongs to (closing day 3) ----------

    [Theory]
    [InlineData(2026, 10, 1, 2026, 10)]   // before the closing day: same month
    [InlineData(2026, 10, 2, 2026, 10)]   // last day before the closing day
    [InlineData(2026, 10, 3, 2026, 11)]   // the closing day already goes to the next cycle
    [InlineData(2026, 10, 31, 2026, 11)]
    [InlineData(2026, 1, 1, 2026, 1)]
    [InlineData(2026, 12, 15, 2027, 1)]   // year rollover
    [InlineData(2026, 12, 31, 2027, 1)]
    [InlineData(2027, 1, 2, 2027, 1)]
    [InlineData(2027, 1, 3, 2027, 2)]
    [InlineData(2026, 2, 28, 2026, 3)]    // end of February, non-leap year
    [InlineData(2028, 2, 29, 2028, 3)]    // leap day
    public void ForPurchase_PicksTheCycleByTheClosingDay(
        int year, int month, int day, int closingYear, int closingMonth)
    {
        var cycle = BillingCycle.ForPurchase(new DateOnly(year, month, day), Rule3);

        Assert.Equal(YearMonth.Of(closingYear, closingMonth), cycle.ClosingMonth);
    }

    [Fact]
    public void ForPurchase_KeepsTheRuleItWasCreatedWith()
    {
        var cycle = BillingCycle.ForPurchase(new DateOnly(2026, 10, 10), Rule3);

        Assert.Equal(Rule3, cycle.Rule);
    }

    // ---------- Cycle dates ----------

    [Fact]
    public void Dates_ClosingInOctober2026_WithClosingDay3AndDueDay12()
    {
        var cycle = Cycle(2026, 10, CardBillingRule.Of(3, 12));

        Assert.Equal(new DateOnly(2026, 9, 3), cycle.StartDate);
        Assert.Equal(new DateOnly(2026, 10, 2), cycle.EndDate);
        Assert.Equal(new DateOnly(2026, 10, 3), cycle.ClosingDate);
        Assert.Equal(new DateOnly(2026, 10, 12), cycle.DueDate);
    }

    [Theory]
    [InlineData(2026, 10, 3, 12, 2026, 10, 12)]  // DueDay > ClosingDay: same month
    [InlineData(2026, 10, 3, 3, 2026, 11, 3)]    // DueDay == ClosingDay: next month
    [InlineData(2026, 10, 25, 5, 2026, 11, 5)]   // DueDay < ClosingDay: next month
    [InlineData(2026, 12, 25, 5, 2027, 1, 5)]    // DueDay < ClosingDay across year rollover
    [InlineData(2026, 12, 10, 10, 2027, 1, 10)]  // DueDay == ClosingDay across year rollover
    public void DueDate_IsFirstOccurrenceOfDueDayAfterClosingDate(
        int closingYear, int closingMonth, int closingDay, int dueDay,
        int expectedDueYear, int expectedDueMonth, int expectedDueDay)
    {
        var cycle = BillingCycle.Closing(
            YearMonth.Of(closingYear, closingMonth),
            CardBillingRule.Of(closingDay, dueDay));

        Assert.Equal(new DateOnly(expectedDueYear, expectedDueMonth, expectedDueDay), cycle.DueDate);
        Assert.Equal(YearMonth.Of(expectedDueYear, expectedDueMonth), cycle.DueMonth);
        Assert.True(cycle.DueDate > cycle.ClosingDate);
    }

    [Fact]
    public void GetStatus_TransitionsAtClosingDateAndDueDate_IncludingDueDateInFollowingMonth()
    {
        var sameMonth = BillingCycle.Closing(YearMonth.Of(2026, 11), CardBillingRule.Of(3, 12)); // closes Nov 3, due Nov 12
        Assert.Equal(InvoiceStatus.Open, sameMonth.GetStatus(new DateOnly(2026, 11, 2)));
        Assert.Equal(InvoiceStatus.Closed, sameMonth.GetStatus(new DateOnly(2026, 11, 3)));
        Assert.Equal(InvoiceStatus.Closed, sameMonth.GetStatus(new DateOnly(2026, 11, 11)));
        Assert.Equal(InvoiceStatus.Paid, sameMonth.GetStatus(new DateOnly(2026, 11, 12)));

        var nextMonth = BillingCycle.Closing(YearMonth.Of(2026, 12), CardBillingRule.Of(25, 5)); // closes Dec 25, due Jan 5 2027
        Assert.Equal(InvoiceStatus.Open, nextMonth.GetStatus(new DateOnly(2026, 12, 24)));
        Assert.Equal(InvoiceStatus.Closed, nextMonth.GetStatus(new DateOnly(2026, 12, 25)));
        Assert.Equal(InvoiceStatus.Closed, nextMonth.GetStatus(new DateOnly(2027, 1, 4)));
        Assert.Equal(InvoiceStatus.Paid, nextMonth.GetStatus(new DateOnly(2027, 1, 5)));
    }

    [Fact]
    public void Dates_ClosingInJanuary_StartInTheYearBefore()
    {
        var cycle = Cycle(2027, 1);

        Assert.Equal(new DateOnly(2026, 12, 3), cycle.StartDate);
        Assert.Equal(new DateOnly(2027, 1, 2), cycle.EndDate);
        Assert.Equal(new DateOnly(2027, 1, 3), cycle.ClosingDate);
    }

    [Fact]
    public void Dates_ClosingInMarch_StartInFebruary()
    {
        var cycle = Cycle(2026, 3);

        Assert.Equal(new DateOnly(2026, 2, 3), cycle.StartDate);
        Assert.Equal(new DateOnly(2026, 3, 2), cycle.EndDate);
    }

    [Fact]
    public void Dates_ClosingDay28_ExistsInFebruaryOfLeapAndNonLeapYears()
    {
        var rule28 = CardBillingRule.Of(28, 28);

        var nonLeap = Cycle(2027, 3, rule28);
        var leap = Cycle(2028, 3, rule28);

        Assert.Equal(new DateOnly(2027, 2, 28), nonLeap.StartDate);
        Assert.Equal(new DateOnly(2027, 3, 27), nonLeap.EndDate);
        Assert.Equal(new DateOnly(2028, 2, 28), leap.StartDate);
        Assert.Equal(new DateOnly(2028, 3, 27), leap.EndDate);
        Assert.Equal(new DateOnly(2028, 3, 28), leap.ClosingDate);
    }

    // ---------- Closing day 1 ----------

    [Fact]
    public void ClosingDay1_CycleGoesFromTheFirstToTheLastDayOfThePreviousMonth()
    {
        var rule1 = CardBillingRule.Of(1, 1);

        var cycle = Cycle(2026, 10, rule1);

        Assert.Equal(new DateOnly(2026, 9, 1), cycle.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 30), cycle.EndDate);
        Assert.Equal(new DateOnly(2026, 10, 1), cycle.ClosingDate);
    }

    [Fact]
    public void ClosingDay1_PurchaseOnTheFirstGoesToTheNextCycle()
    {
        var rule1 = CardBillingRule.Of(1, 1);

        var onFirst = BillingCycle.ForPurchase(new DateOnly(2026, 10, 1), rule1);
        var lastOfPreviousMonth = BillingCycle.ForPurchase(new DateOnly(2026, 9, 30), rule1);

        Assert.Equal(YearMonth.Of(2026, 11), onFirst.ClosingMonth);
        Assert.Equal(YearMonth.Of(2026, 10), lastOfPreviousMonth.ClosingMonth);
    }

    // ---------- AddCycles ----------

    [Theory]
    [InlineData(0, 2026, 10)]
    [InlineData(1, 2026, 11)]
    [InlineData(12, 2027, 10)]
    [InlineData(14, 2027, 12)]
    [InlineData(-1, 2026, 9)]
    [InlineData(-10, 2025, 12)]
    public void AddCycles_MovesTheClosingMonth_AndKeepsTheRule(
        int cycles, int expectedYear, int expectedMonth)
    {
        var result = Cycle(2026, 10).AddCycles(cycles);

        Assert.Equal(YearMonth.Of(expectedYear, expectedMonth), result.ClosingMonth);
        Assert.Equal(Rule3, result.Rule);
    }

    [Theory]
    [InlineData(1, 2027, 1)]
    [InlineData(2, 2027, 2)]
    [InlineData(3, 2027, 3)]
    [InlineData(12, 2027, 12)]
    public void AddCycles_InstallmentKBelongsToTheFirstCyclePlusKMinusOne(
        int installment, int expectedYear, int expectedMonth)
    {
        // Purchase on 15/12/2026 closes in January 2027.
        var first = BillingCycle.ForPurchase(new DateOnly(2026, 12, 15), Rule3);

        var cycle = first.AddCycles(installment - 1);

        Assert.Equal(YearMonth.Of(expectedYear, expectedMonth), cycle.ClosingMonth);
    }

    // ---------- Equality ----------

    [Fact]
    public void Equality_SameMonthAndRule_AreEqualWithSameHash()
    {
        var a = Cycle(2026, 10);
        var b = Cycle(2026, 10, CardBillingRule.Of(3, 3));

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentMonthOrRule_AreNotEqual()
    {
        Assert.NotEqual(Cycle(2026, 10), Cycle(2026, 11));
        Assert.NotEqual(Cycle(2026, 10), Cycle(2026, 10, CardBillingRule.Of(4, 4)));
    }

    // ---------- Guards and limits ----------

    [Fact]
    public void Closing_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => BillingCycle.Closing(null!, Rule3));
        Assert.Throws<ArgumentNullException>(() => BillingCycle.Closing(YearMonth.Of(2026, 10), null!));
    }

    [Fact]
    public void ForPurchase_NullRule_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => BillingCycle.ForPurchase(new DateOnly(2026, 10, 10), null!));
    }

    [Fact]
    public void Closing_FirstSupportedMonth_IsRejected_ButTheSecondIsAccepted()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Cycle(1, 1));

        var cycle = Cycle(1, 2);

        Assert.Equal(new DateOnly(1, 1, 3), cycle.StartDate);
    }

    [Theory]
    [InlineData(3, 3, true)]    // due day equal to the closing day: due date would be in January 10000
    [InlineData(3, 1, true)]    // due day before the closing day
    [InlineData(28, 12, true)]
    [InlineData(3, 4, false)]   // due day after the closing day: stays in December 9999
    [InlineData(3, 12, false)]
    public void Closing_InTheLastSupportedMonth_RejectsADueDateInTheFollowingYear(
    int closingDay, int dueDay, bool rejected)
    {
        var rule = CardBillingRule.Of(closingDay, dueDay);
        var month = YearMonth.Of(9999, 12);

        if (rejected)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BillingCycle.Closing(month, rule));
        }
        else
        {
            Assert.Equal(new DateOnly(9999, 12, dueDay), BillingCycle.Closing(month, rule).DueDate);
        }
    }

    [Fact]
    public void ForPurchase_AtTheLowerLimit_ThrowsWithClosingDay3_ButWorksWithClosingDay1()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BillingCycle.ForPurchase(DateOnly.MinValue, Rule3));

        var cycle = BillingCycle.ForPurchase(DateOnly.MinValue, CardBillingRule.Of(1, 1));

        Assert.Equal(YearMonth.Of(1, 2), cycle.ClosingMonth);
        Assert.Equal(new DateOnly(1, 1, 1), cycle.StartDate);
        Assert.Equal(new DateOnly(1, 1, 31), cycle.EndDate);
    }

    [Fact]
    public void ForPurchase_AtTheUpperLimit_WorksBeforeTheClosingDay_AndThrowsFromIt()
    {
        // The due day (12) comes after the closing day (3), so the due date stays in December 9999.
        var rule = CardBillingRule.Of(3, 12);

        var cycle = BillingCycle.ForPurchase(new DateOnly(9999, 12, 2), rule);

        Assert.Equal(YearMonth.Of(9999, 12), cycle.ClosingMonth);
        Assert.Equal(new DateOnly(9999, 12, 3), cycle.ClosingDate);
        Assert.Equal(new DateOnly(9999, 12, 2), cycle.EndDate);
        Assert.Equal(new DateOnly(9999, 12, 12), cycle.DueDate);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => BillingCycle.ForPurchase(new DateOnly(9999, 12, 3), rule));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BillingCycle.ForPurchase(new DateOnly(9999, 12, 31), rule));
    }

    [Fact]
    public void AddCycles_OutsideTheSupportedRange_Throws()
    {
        // Use a valid 9999-12 cycle (DueDay > ClosingDay) so AddCycles(1) is actually invoked.
        var lastValidCycle = Cycle(9999, 12, CardBillingRule.Of(3, 12));
        var firstValidCycle = Cycle(1, 2, CardBillingRule.Of(3, 12));
        var midCycle = Cycle(2026, 10, CardBillingRule.Of(3, 12));

        Assert.Throws<ArgumentOutOfRangeException>(() => lastValidCycle.AddCycles(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => firstValidCycle.AddCycles(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => midCycle.AddCycles(int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => midCycle.AddCycles(int.MinValue));
    }

    // ---------- Main invariants ----------

    [Fact]
    public void EveryDate_BelongsToTheCycleItWasAssigned_ForEveryClosingDay()
    {
        var firstDate = new DateOnly(2027, 1, 1);
        var lastDate = new DateOnly(2028, 12, 31);

        for (var closingDay = CardBillingRule.MinClosingDay;
             closingDay <= CardBillingRule.MaxClosingDay;
             closingDay++)
        {
            var rule = CardBillingRule.Of(closingDay, closingDay);

            for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
            {
                var cycle = BillingCycle.ForPurchase(date, rule);
                var context = $"closingDay={closingDay}, date={date:yyyy-MM-dd}";

                Assert.True(
                    cycle.StartDate <= date && date <= cycle.EndDate,
                    $"Date is outside its own cycle: {context}");
                Assert.True(date < cycle.ClosingDate, $"Date is not before the closing date: {context}");

                // The next day stays in the same cycle, except right after EndDate.
                var next = BillingCycle.ForPurchase(date.AddDays(1), rule);
                var expectedNext = date == cycle.EndDate ? cycle.AddCycles(1) : cycle;

                Assert.True(next == expectedNext, $"Next day is in the wrong cycle: {context}");
            }
        }
    }

    [Fact]
    public void ConsecutiveCycles_HaveNoGapAndNoOverlap_AndLastAsLongAsThePreviousMonth()
    {
        for (var closingDay = CardBillingRule.MinClosingDay;
             closingDay <= CardBillingRule.MaxClosingDay;
             closingDay++)
        {
            var rule = CardBillingRule.Of(closingDay, closingDay);
            var current = Cycle(2000, 1, rule);

            for (var i = 0; i < 1200; i++)
            {
                var next = current.AddCycles(1);
                var context = $"closingDay={closingDay}, closing={current.ClosingMonth}";

                Assert.True(
                    next.StartDate == current.EndDate.AddDays(1),
                    $"Gap or overlap between cycles: {context}");
                Assert.True(
                    next.StartDate == current.ClosingDate,
                    $"Next cycle must start on the closing date: {context}");

                var previousMonth = current.ClosingMonth.AddMonths(-1);
                var length = current.EndDate.DayNumber - current.StartDate.DayNumber + 1;
                Assert.True(
                    length == DateTime.DaysInMonth(previousMonth.Year, previousMonth.Month),
                    $"Cycle length differs from the previous month length: {context}");

                current = next;
            }
        }
    }

    [Theory]
    [InlineData(2026, 12, 15, 1, 2027, 2, 3)]
    [InlineData(2026, 10, 2, 5, 2027, 3, 3)]
    [InlineData(2026, 10, 3, 24, 2028, 11, 3)]
    [InlineData(2028, 2, 29, 12, 2029, 3, 3)]
    public void AddCycles_ShiftsClosingDateByExactCalendarMonths(
        int year, int month, int day, int cycles, int expectedYear, int expectedMonth, int expectedDay)
    {
        var first = BillingCycle.ForPurchase(new DateOnly(year, month, day), CardBillingRule.Of(3, 12));

        var shifted = first.AddCycles(cycles);

        Assert.Equal(YearMonth.Of(expectedYear, expectedMonth), shifted.ClosingMonth);
        Assert.Equal(new DateOnly(expectedYear, expectedMonth, expectedDay), shifted.ClosingDate);
        Assert.Equal(new DateOnly(expectedYear, expectedMonth, 12), shifted.DueDate);
    }
}