using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.Entities;

public class FixedExpenseTests
{
    private static readonly CardBillingRule Rule3 = CardBillingRule.Of(3, 3);
    private static readonly YearMonth DefaultStart = YearMonth.Of(2026, 1);

    private static Money M(decimal amount) => Money.FromDecimal(amount);

    private static Money? Amt(decimal amount) => M(amount);

    private static YearMonth YM(int year, int month) => YearMonth.Of(year, month);

    private static DateOnly Day(int year, int month, int day) => new(year, month, day);

    private static DateOnly? OptDay(int year, int month, int day) => new DateOnly(year, month, day);

    private static FixedExpense NewExpense(
        YearMonth? start = null,
        YearMonth? end = null,
        decimal amount = 1000.00m,
        string description = "Aluguel",
        ExpenseCategory category = ExpenseCategory.Services) =>
        FixedExpense.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            start ?? DefaultStart,
            end,
            M(amount),
            description,
            category);

    // ---------- Create ----------

    [Fact]
    public void Create_StoresTheGivenValues_AndTrimsTheDescription()
    {
        var id = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var expense = FixedExpense.Create(
            id, accountId, YM(2026, 10), YM(2027, 3), M(1500.00m), "  Aluguel  ", ExpenseCategory.Services);

        Assert.Equal(id, expense.Id);
        Assert.Equal(accountId, expense.AccountId);
        Assert.Equal(YM(2026, 10), expense.StartMonth);
        Assert.Equal(YM(2027, 3), expense.EndMonth);
        Assert.Equal("Aluguel", expense.Description);
        Assert.Equal(ExpenseCategory.Services, expense.Category);
    }

    [Fact]
    public void Create_StartsWithASingleAmountEntryAtTheStartMonth_AndNoPayments()
    {
        var expense = NewExpense(start: YM(2026, 10), amount: 1500.00m);

        var entry = Assert.Single(expense.AmountHistory);
        Assert.Equal(YM(2026, 10), entry.EffectiveFrom);
        Assert.Equal(M(1500.00m), entry.Amount);
        Assert.Empty(expense.Payments);
        Assert.Null(expense.EndMonth);
    }

    [Fact]
    public void Create_EndMonthEqualToStartMonth_IsAccepted()
    {
        var expense = NewExpense(start: YM(2026, 10), end: YM(2026, 10));

        Assert.True(expense.IsActiveIn(YM(2026, 10)));
        Assert.False(expense.IsActiveIn(YM(2026, 11)));
    }

    [Fact]
    public void Create_EndMonthBeforeStartMonth_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NewExpense(start: YM(2026, 10), end: YM(2026, 9)));
    }

    [Fact]
    public void Create_NullStartMonth_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => FixedExpense.Create(
            Guid.NewGuid(), Guid.NewGuid(), null!, null, M(10m), "x", ExpenseCategory.Services));
    }

    [Fact]
    public void Create_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() => FixedExpense.Create(
            Guid.Empty, Guid.NewGuid(), DefaultStart, null, M(10m), "x", ExpenseCategory.Services));
    }

    [Fact]
    public void Create_EmptyAccountId_Throws()
    {
        Assert.Throws<ArgumentException>(() => FixedExpense.Create(
            Guid.NewGuid(), Guid.Empty, DefaultStart, null, M(10m), "x", ExpenseCategory.Services));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public void Create_ZeroOrNegativeAmount_Throws(double raw)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewExpense(amount: (decimal)raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_DescriptionIsRequired(string? description)
    {
        Assert.ThrowsAny<ArgumentException>(() => NewExpense(description: description!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(99)]
    public void Create_UnknownCategory_Throws(int category)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NewExpense(category: (ExpenseCategory)category));
    }

    [Fact]
    public void Create_AcceptsEveryKnownCategory()
    {
        foreach (var category in Enum.GetValues<ExpenseCategory>())
        {
            Assert.Equal(category, NewExpense(category: category).Category);
        }
    }

    // ---------- FirstOpenMonth ----------

    [Theory]
    [InlineData(2026, 10, 1, 2026, 10)]
    [InlineData(2026, 10, 3, 2026, 10)]
    [InlineData(2026, 10, 4, 2026, 11)]
    [InlineData(2026, 10, 31, 2026, 11)]
    [InlineData(2026, 12, 3, 2026, 12)]
    [InlineData(2026, 12, 4, 2027, 1)]
    [InlineData(2026, 12, 31, 2027, 1)]
    public void FirstOpenMonth_ClosingDay3_IsOpenThroughTheClosingDayInclusive(
        int year, int month, int day, int expectedYear, int expectedMonth)
    {
        var result = FixedExpense.FirstOpenMonth(Day(year, month, day), Rule3);

        Assert.Equal(YM(expectedYear, expectedMonth), result);
    }

    [Theory]
    [InlineData(1, 2026, 10, 1, 2026, 10)]
    [InlineData(1, 2026, 10, 2, 2026, 11)]
    [InlineData(28, 2026, 10, 28, 2026, 10)]
    [InlineData(28, 2026, 10, 29, 2026, 11)]
    [InlineData(15, 2026, 2, 15, 2026, 2)]
    [InlineData(15, 2026, 2, 16, 2026, 3)]
    public void FirstOpenMonth_FollowsTheClosingDayOfTheGivenRule(
        int closingDay, int year, int month, int day, int expectedYear, int expectedMonth)
    {
        var result = FixedExpense.FirstOpenMonth(Day(year, month, day), CardBillingRule.Of(closingDay, closingDay));

        Assert.Equal(YM(expectedYear, expectedMonth), result);
    }

    [Fact]
    public void FirstOpenMonth_NullRule_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => FixedExpense.FirstOpenMonth(Day(2026, 10, 10), null!));
    }

    // ---------- IsActiveIn ----------

    [Fact]
    public void IsActiveIn_WithoutEnd_IsActiveFromTheStartMonthOnward()
    {
        var expense = NewExpense(start: YM(2026, 10));

        Assert.False(expense.IsActiveIn(YM(2026, 9)));
        Assert.True(expense.IsActiveIn(YM(2026, 10)));
        Assert.True(expense.IsActiveIn(YM(2026, 11)));
        Assert.True(expense.IsActiveIn(YM(2040, 1)));
    }

    [Fact]
    public void IsActiveIn_WithEnd_IsActiveOnlyBetweenStartAndEndInclusive()
    {
        var expense = NewExpense(start: YM(2026, 10), end: YM(2026, 12));

        Assert.False(expense.IsActiveIn(YM(2026, 9)));
        Assert.True(expense.IsActiveIn(YM(2026, 10)));
        Assert.True(expense.IsActiveIn(YM(2026, 11)));
        Assert.True(expense.IsActiveIn(YM(2026, 12)));
        Assert.False(expense.IsActiveIn(YM(2027, 1)));
    }

    [Fact]
    public void IsActiveIn_NullMonth_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => NewExpense().IsActiveIn(null!));
    }

    // ---------- AmountIn ----------

    [Fact]
    public void AmountIn_InactiveMonths_AreNull()
    {
        var expense = NewExpense(start: YM(2026, 10), end: YM(2026, 12));

        Assert.Null(expense.AmountIn(YM(2026, 9)));
        Assert.Null(expense.AmountIn(YM(2027, 1)));
    }

    [Fact]
    public void AmountIn_WithoutChanges_IsTheInitialAmountInEveryActiveMonth()
    {
        var expense = NewExpense(start: YM(2026, 10), end: YM(2026, 12), amount: 1500.00m);

        Assert.Equal(Amt(1500.00m), expense.AmountIn(YM(2026, 10)));
        Assert.Equal(Amt(1500.00m), expense.AmountIn(YM(2026, 11)));
        Assert.Equal(Amt(1500.00m), expense.AmountIn(YM(2026, 12)));
    }

    [Fact]
    public void AmountIn_AfterSeveralChanges_UsesTheLatestEntryNotAfterTheMonth()
    {
        var expense = NewExpense(start: YM(2026, 1), amount: 1000.00m);

        expense.ChangeAmount(M(1100.00m), Day(2026, 3, 2), Rule3);  // from March
        expense.ChangeAmount(M(1200.00m), Day(2026, 6, 10), Rule3); // from July

        Assert.Equal(Amt(1000.00m), expense.AmountIn(YM(2026, 2)));
        Assert.Equal(Amt(1100.00m), expense.AmountIn(YM(2026, 3)));
        Assert.Equal(Amt(1100.00m), expense.AmountIn(YM(2026, 6)));
        Assert.Equal(Amt(1200.00m), expense.AmountIn(YM(2026, 7)));
        Assert.Equal(Amt(1200.00m), expense.AmountIn(YM(2027, 5)));
        Assert.Equal(
            new[] { YM(2026, 1), YM(2026, 3), YM(2026, 7) },
            expense.AmountHistory.Select(change => change.EffectiveFrom));
    }

    // ---------- ChangeAmount ----------

    [Fact]
    public void ChangeAmount_OnTheClosingDay_AppliesFromTheCurrentMonth()
    {
        var expense = NewExpense(start: YM(2026, 1), amount: 1000.00m);

        var effectiveFrom = expense.ChangeAmount(M(1500.00m), Day(2026, 10, 3), Rule3);

        Assert.Equal(YM(2026, 10), effectiveFrom);
        Assert.Equal(Amt(1000.00m), expense.AmountIn(YM(2026, 9)));
        Assert.Equal(Amt(1500.00m), expense.AmountIn(YM(2026, 10)));
    }

    [Fact]
    public void ChangeAmount_AfterTheClosingDay_AppliesFromTheNextMonth()
    {
        var expense = NewExpense(start: YM(2026, 1), amount: 1000.00m);

        var effectiveFrom = expense.ChangeAmount(M(1500.00m), Day(2026, 10, 4), Rule3);

        Assert.Equal(YM(2026, 11), effectiveFrom);
        Assert.Equal(Amt(1000.00m), expense.AmountIn(YM(2026, 10)));
        Assert.Equal(Amt(1500.00m), expense.AmountIn(YM(2026, 11)));
    }

    [Fact]
    public void ChangeAmount_AfterTheClosingDayInDecember_RollsOverTheYear()
    {
        var expense = NewExpense(start: YM(2026, 1), amount: 1000.00m);

        var effectiveFrom = expense.ChangeAmount(M(1500.00m), Day(2026, 12, 10), Rule3);

        Assert.Equal(YM(2027, 1), effectiveFrom);
        Assert.Equal(Amt(1000.00m), expense.AmountIn(YM(2026, 12)));
        Assert.Equal(Amt(1500.00m), expense.AmountIn(YM(2027, 1)));
    }

    [Fact]
    public void ChangeAmount_FollowsTheClosingDayOfTheGivenRule()
    {
        var rule15 = CardBillingRule.Of(15, 15);
        var expense = NewExpense(start: YM(2026, 1));

        // With closing day 3 this would already apply from November.
        var effectiveFrom = expense.ChangeAmount(M(1500.00m), Day(2026, 10, 10), rule15);

        Assert.Equal(YM(2026, 10), effectiveFrom);
    }

    [Fact]
    public void ChangeAmount_BeforeTheExpenseStarts_AppliesFromTheStartMonth()
    {
        var expense = NewExpense(start: YM(2027, 3), amount: 1000.00m);

        var effectiveFrom = expense.ChangeAmount(M(1500.00m), Day(2026, 10, 10), Rule3);

        Assert.Equal(YM(2027, 3), effectiveFrom);
        var entry = Assert.Single(expense.AmountHistory);
        Assert.Equal(YM(2027, 3), entry.EffectiveFrom);
        Assert.Equal(M(1500.00m), entry.Amount);
        Assert.Equal(Amt(1500.00m), expense.AmountIn(YM(2027, 3)));
    }

    [Fact]
    public void ChangeAmount_TwiceInTheSameOpenWindow_TheLastEditWins()
    {
        var expense = NewExpense(start: YM(2026, 1), amount: 1000.00m);

        expense.ChangeAmount(M(1500.00m), Day(2026, 10, 2), Rule3);
        expense.ChangeAmount(M(1600.00m), Day(2026, 10, 3), Rule3);

        Assert.Equal(Amt(1600.00m), expense.AmountIn(YM(2026, 10)));
        Assert.Equal(2, expense.AmountHistory.Count);
    }

    [Fact]
    public void ChangeAmount_DropsChangesScheduledForLaterMonths()
    {
        var expense = NewExpense(start: YM(2026, 1), amount: 1000.00m);
        expense.ChangeAmount(M(1200.00m), Day(2026, 10, 10), Rule3); // from November

        expense.ChangeAmount(M(1300.00m), Day(2026, 10, 2), Rule3);  // from October

        Assert.Equal(
            new[] { YM(2026, 1), YM(2026, 10) },
            expense.AmountHistory.Select(change => change.EffectiveFrom));
        Assert.Equal(Amt(1300.00m), expense.AmountIn(YM(2026, 11)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void ChangeAmount_ZeroOrNegative_ThrowsAndKeepsTheHistory(double raw)
    {
        var expense = NewExpense();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => expense.ChangeAmount(M((decimal)raw), Day(2026, 10, 10), Rule3));

        Assert.Single(expense.AmountHistory);
    }

    [Fact]
    public void ChangeAmount_NullRule_Throws()
    {
        var expense = NewExpense();

        Assert.Throws<ArgumentNullException>(
            () => expense.ChangeAmount(M(10m), Day(2026, 10, 10), null!));
    }

    [Fact]
    public void ChangeAmount_WhenTheExpenseHasAlreadyEndedForTheOpenMonths_Throws()
    {
        var expense = NewExpense(start: YM(2026, 1), end: YM(2026, 8));

        Assert.Throws<InvalidOperationException>(
            () => expense.ChangeAmount(M(1500.00m), Day(2026, 10, 10), Rule3));

        Assert.Single(expense.AmountHistory);
    }

    [Fact]
    public void ChangeAmount_WhenTheLastActiveMonthIsStillOpen_IsAccepted()
    {
        var expense = NewExpense(start: YM(2026, 1), end: YM(2026, 11));

        var effectiveFrom = expense.ChangeAmount(M(1500.00m), Day(2026, 10, 10), Rule3);

        Assert.Equal(YM(2026, 11), effectiveFrom);
        Assert.Equal(Amt(1500.00m), expense.AmountIn(YM(2026, 11)));
    }

    [Fact]
    public void ChangeAmount_WhenAnAffectedMonthIsPaid_ThrowsAndKeepsTheHistory()
    {
        var expense = NewExpense(start: YM(2026, 1));
        expense.MarkAsPaid(YM(2026, 10), Day(2026, 10, 1));

        // Before the closing day the change would apply from October, which is already paid.
        Assert.Throws<InvalidOperationException>(
            () => expense.ChangeAmount(M(1500.00m), Day(2026, 10, 2), Rule3));

        Assert.Single(expense.AmountHistory);
    }

    [Fact]
    public void ChangeAmount_WhenOnlyEarlierMonthsArePaid_IsAccepted()
    {
        var expense = NewExpense(start: YM(2026, 1));
        expense.MarkAsPaid(YM(2026, 10), Day(2026, 10, 1));

        // After the closing day the change applies from November.
        expense.ChangeAmount(M(1500.00m), Day(2026, 10, 10), Rule3);

        Assert.Equal(Amt(1000.00m), expense.AmountIn(YM(2026, 10)));
        Assert.Equal(Amt(1500.00m), expense.AmountIn(YM(2026, 11)));
    }

    [Fact]
    public void ChangeAmount_WhenALaterMonthWasPaidInAdvance_Throws()
    {
        var expense = NewExpense(start: YM(2026, 1));
        expense.MarkAsPaid(YM(2026, 12), Day(2026, 10, 5));

        Assert.Throws<InvalidOperationException>(
            () => expense.ChangeAmount(M(1500.00m), Day(2026, 10, 10), Rule3));
    }

    [Fact]
    public void ChangeAmount_AfterRevertingThePayment_IsAccepted()
    {
        var expense = NewExpense(start: YM(2026, 1));
        expense.MarkAsPaid(YM(2026, 10), Day(2026, 10, 1));
        expense.RevertPayment(YM(2026, 10));

        expense.ChangeAmount(M(1500.00m), Day(2026, 10, 2), Rule3);

        Assert.Equal(Amt(1500.00m), expense.AmountIn(YM(2026, 10)));
    }

    // ---------- End ----------

    [Fact]
    public void End_OpenEnded_SetsTheLastMonth()
    {
        var expense = NewExpense(start: YM(2026, 1));

        expense.End(YM(2026, 12), Day(2026, 10, 10), Rule3);

        Assert.Equal(YM(2026, 12), expense.EndMonth);
        Assert.True(expense.IsActiveIn(YM(2026, 12)));
        Assert.False(expense.IsActiveIn(YM(2027, 1)));
        Assert.Null(expense.AmountIn(YM(2027, 1)));
    }

    [Theory]
    [InlineData(2026, 10, 10, 2026, 10, true)]   // stops from November, which is open
    [InlineData(2026, 10, 10, 2026, 9, false)]   // would stop October, already frozen
    [InlineData(2026, 10, 3, 2026, 9, true)]     // on the closing day October is still open
    [InlineData(2026, 10, 3, 2026, 8, false)]
    [InlineData(2026, 10, 4, 2026, 10, true)]
    [InlineData(2026, 10, 4, 2026, 9, false)]
    public void End_OnlyStopsMonthsThatAreStillOpen(
        int todayYear, int todayMonth, int todayDay, int lastYear, int lastMonth, bool allowed)
    {
        var expense = NewExpense(start: YM(2026, 1));
        var today = Day(todayYear, todayMonth, todayDay);

        if (allowed)
        {
            expense.End(YM(lastYear, lastMonth), today, Rule3);

            Assert.Equal(YM(lastYear, lastMonth), expense.EndMonth);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(
                () => expense.End(YM(lastYear, lastMonth), today, Rule3));

            Assert.Null(expense.EndMonth);
        }
    }

    [Fact]
    public void End_LastMonthBeforeTheStartMonth_Throws()
    {
        var expense = NewExpense(start: YM(2026, 10));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => expense.End(YM(2026, 9), Day(2026, 10, 10), Rule3));
    }

    [Fact]
    public void End_NullLastMonth_Throws()
    {
        var expense = NewExpense();

        Assert.Throws<ArgumentNullException>(() => expense.End(null!, Day(2026, 10, 10), Rule3));
    }

    [Fact]
    public void End_WithTheSameEndMonth_DoesNothing_EvenWhenFrozen()
    {
        var expense = NewExpense(start: YM(2026, 1), end: YM(2026, 3));

        expense.End(YM(2026, 3), Day(2026, 10, 10), Rule3);

        Assert.Equal(YM(2026, 3), expense.EndMonth);
    }

    [Fact]
    public void End_ExtendingOverFrozenMonths_Throws()
    {
        var expense = NewExpense(start: YM(2026, 1), end: YM(2026, 8));

        // September and October are already frozen, so they cannot become active again.
        Assert.Throws<InvalidOperationException>(
            () => expense.End(YM(2026, 12), Day(2026, 10, 10), Rule3));

        Assert.Equal(YM(2026, 8), expense.EndMonth);
    }

    [Fact]
    public void End_ExtendingOverOpenMonths_IsAccepted()
    {
        var expense = NewExpense(start: YM(2026, 1), end: YM(2026, 11));

        expense.End(YM(2026, 12), Day(2026, 10, 10), Rule3);

        Assert.Equal(YM(2026, 12), expense.EndMonth);
    }

    [Fact]
    public void End_ShorteningOverOpenMonths_IsAccepted()
    {
        var expense = NewExpense(start: YM(2026, 1), end: YM(2026, 12));

        expense.End(YM(2026, 11), Day(2026, 10, 10), Rule3);

        Assert.Equal(YM(2026, 11), expense.EndMonth);
        Assert.False(expense.IsActiveIn(YM(2026, 12)));
    }

    [Fact]
    public void End_WhenALaterMonthIsPaid_ThrowsUntilThePaymentIsReverted()
    {
        var expense = NewExpense(start: YM(2026, 1));
        expense.MarkAsPaid(YM(2026, 12), Day(2026, 10, 5));

        Assert.Throws<InvalidOperationException>(
            () => expense.End(YM(2026, 11), Day(2026, 10, 10), Rule3));
        Assert.Null(expense.EndMonth);

        expense.RevertPayment(YM(2026, 12));
        expense.End(YM(2026, 11), Day(2026, 10, 10), Rule3);

        Assert.Equal(YM(2026, 11), expense.EndMonth);
    }

    [Fact]
    public void End_WhenTheLastMonthItselfIsPaid_IsAccepted()
    {
        var expense = NewExpense(start: YM(2026, 1));
        expense.MarkAsPaid(YM(2026, 11), Day(2026, 10, 5));

        expense.End(YM(2026, 11), Day(2026, 10, 10), Rule3);

        Assert.Equal(YM(2026, 11), expense.EndMonth);
        Assert.True(expense.IsPaid(YM(2026, 11)));
    }

    // ---------- MarkAsPaid ----------

    [Fact]
    public void MarkAsPaid_ActiveMonth_BecomesPaidAndReturnsTrue()
    {
        var expense = NewExpense(start: YM(2026, 1));

        var changed = expense.MarkAsPaid(YM(2026, 10), Day(2026, 10, 5));

        Assert.True(changed);
        Assert.True(expense.IsPaid(YM(2026, 10)));
        Assert.Equal(OptDay(2026, 10, 5), expense.PaidOn(YM(2026, 10)));
    }

    [Fact]
    public void MarkAsPaid_Twice_WithTheSameDate_ReturnsFalseAndKeepsTheState()
    {
        var expense = NewExpense(start: YM(2026, 1));
        expense.MarkAsPaid(YM(2026, 10), Day(2026, 10, 5));

        var changed = expense.MarkAsPaid(YM(2026, 10), Day(2026, 10, 5));

        Assert.False(changed);
        Assert.Single(expense.Payments);
    }

    [Fact]
    public void MarkAsPaid_Twice_WithAnotherDate_KeepsTheOriginalDate()
    {
        var expense = NewExpense(start: YM(2026, 1));
        expense.MarkAsPaid(YM(2026, 10), Day(2026, 10, 5));

        var changed = expense.MarkAsPaid(YM(2026, 10), Day(2026, 11, 20));

        Assert.False(changed);
        Assert.Equal(OptDay(2026, 10, 5), expense.PaidOn(YM(2026, 10)));
    }

    [Fact]
    public void MarkAsPaid_RepeatedManyTimes_NeverChangesTheFirstDate()
    {
        var expense = NewExpense(start: YM(2026, 1));
        Assert.True(expense.MarkAsPaid(YM(2026, 10), Day(2026, 10, 5)));

        for (var day = 1; day <= 28; day++)
        {
            Assert.False(expense.MarkAsPaid(YM(2026, 10), Day(2026, 12, day)));
        }

        Assert.Equal(OptDay(2026, 10, 5), expense.PaidOn(YM(2026, 10)));
    }

    [Fact]
    public void MarkAsPaid_MonthsAreIndependent()
    {
        var expense = NewExpense(start: YM(2026, 1));

        expense.MarkAsPaid(YM(2026, 10), Day(2026, 10, 5));

        Assert.True(expense.IsPaid(YM(2026, 10)));
        Assert.False(expense.IsPaid(YM(2026, 11)));
        Assert.Null(expense.PaidOn(YM(2026, 11)));
    }

    [Fact]
    public void MarkAsPaid_PaidInAnotherMonthThanTheCompetence_KeepsThePaymentDate()
    {
        // October's expense paid on 02/11: October by accrual, November by cash.
        var expense = NewExpense(start: YM(2026, 1));

        expense.MarkAsPaid(YM(2026, 10), Day(2026, 11, 2));

        Assert.Equal(OptDay(2026, 11, 2), expense.PaidOn(YM(2026, 10)));
        Assert.Null(expense.PaidOn(YM(2026, 11)));
    }

    [Fact]
    public void MarkAsPaid_InAdvance_IsAccepted()
    {
        var expense = NewExpense(start: YM(2026, 1));

        Assert.True(expense.MarkAsPaid(YM(2026, 12), Day(2026, 10, 5)));
    }

    [Fact]
    public void MarkAsPaid_InactiveMonth_ThrowsAndRecordsNothing()
    {
        var expense = NewExpense(start: YM(2026, 10), end: YM(2026, 12));

        Assert.Throws<InvalidOperationException>(
            () => expense.MarkAsPaid(YM(2026, 9), Day(2026, 10, 5)));
        Assert.Throws<InvalidOperationException>(
            () => expense.MarkAsPaid(YM(2027, 1), Day(2026, 10, 5)));

        Assert.Empty(expense.Payments);
    }

    [Fact]
    public void MarkAsPaid_NullMonth_Throws()
    {
        var expense = NewExpense();

        Assert.Throws<ArgumentNullException>(() => expense.MarkAsPaid(null!, Day(2026, 10, 5)));
    }

    // ---------- RevertPayment ----------

    [Fact]
    public void RevertPayment_PaidMonth_BecomesUnpaidAndReturnsTrue()
    {
        var expense = NewExpense(start: YM(2026, 1));
        expense.MarkAsPaid(YM(2026, 10), Day(2026, 10, 5));

        var changed = expense.RevertPayment(YM(2026, 10));

        Assert.True(changed);
        Assert.False(expense.IsPaid(YM(2026, 10)));
        Assert.Null(expense.PaidOn(YM(2026, 10)));
    }

    [Fact]
    public void RevertPayment_Twice_ReturnsFalseTheSecondTime()
    {
        var expense = NewExpense(start: YM(2026, 1));
        expense.MarkAsPaid(YM(2026, 10), Day(2026, 10, 5));

        Assert.True(expense.RevertPayment(YM(2026, 10)));
        Assert.False(expense.RevertPayment(YM(2026, 10)));
    }

    [Fact]
    public void RevertPayment_WhenNeverPaid_ReturnsFalse()
    {
        Assert.False(NewExpense().RevertPayment(YM(2026, 10)));
    }

    [Fact]
    public void RevertPayment_IsAllowedForFrozenMonths()
    {
        var expense = NewExpense(start: YM(2026, 1));
        expense.MarkAsPaid(YM(2026, 3), Day(2026, 3, 5));

        Assert.True(expense.RevertPayment(YM(2026, 3)));
    }

    [Fact]
    public void AfterReverting_CanBePaidAgainWithAnotherDate()
    {
        var expense = NewExpense(start: YM(2026, 1));
        expense.MarkAsPaid(YM(2026, 10), Day(2026, 10, 5));
        expense.RevertPayment(YM(2026, 10));

        var changed = expense.MarkAsPaid(YM(2026, 10), Day(2026, 10, 9));

        Assert.True(changed);
        Assert.Equal(OptDay(2026, 10, 9), expense.PaidOn(YM(2026, 10)));
    }

    [Fact]
    public void RevertPayment_NullMonth_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => NewExpense().RevertPayment(null!));
    }

    // ---------- IsPaid and PaidOn ----------

    [Fact]
    public void IsPaid_AndPaidOn_NullMonth_Throw()
    {
        var expense = NewExpense();

        Assert.Throws<ArgumentNullException>(() => expense.IsPaid(null!));
        Assert.Throws<ArgumentNullException>(() => expense.PaidOn(null!));
    }

    // ---------- UpdateDescription and UpdateCategory ----------

    [Fact]
    public void UpdateDescription_ChangesAndTrims()
    {
        var expense = NewExpense();

        expense.UpdateDescription("  Internet  ");

        Assert.Equal("Internet", expense.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDescription_Empty_ThrowsAndKeepsTheDescription(string? description)
    {
        var expense = NewExpense(description: "Aluguel");

        Assert.ThrowsAny<ArgumentException>(() => expense.UpdateDescription(description!));

        Assert.Equal("Aluguel", expense.Description);
    }

    [Fact]
    public void UpdateCategory_Changes()
    {
        var expense = NewExpense(category: ExpenseCategory.Services);

        expense.UpdateCategory(ExpenseCategory.Education);

        Assert.Equal(ExpenseCategory.Education, expense.Category);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void UpdateCategory_Unknown_ThrowsAndKeepsTheCategory(int category)
    {
        var expense = NewExpense(category: ExpenseCategory.Services);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => expense.UpdateCategory((ExpenseCategory)category));

        Assert.Equal(ExpenseCategory.Services, expense.Category);
    }

    [Fact]
    public void DescriptionAndCategory_RemainEditable_ForFrozenAndPaidExpenses()
    {
        var expense = NewExpense(start: YM(2026, 1), end: YM(2026, 3));
        expense.MarkAsPaid(YM(2026, 3), Day(2026, 3, 5));

        expense.UpdateDescription("Aluguel antigo");
        expense.UpdateCategory(ExpenseCategory.Personal);

        Assert.Equal("Aluguel antigo", expense.Description);
        Assert.Equal(ExpenseCategory.Personal, expense.Category);
    }

    // ---------- Encapsulation ----------

    [Fact]
    public void ExposedCollections_CannotBeModifiedByTheCaller()
    {
        var expense = NewExpense();
        expense.MarkAsPaid(YM(2026, 10), Day(2026, 10, 5));

        Assert.Throws<NotSupportedException>(
            () => ((IList<AmountChange>)expense.AmountHistory).Clear());
        Assert.Throws<NotSupportedException>(
            () => ((IDictionary<YearMonth, DateOnly>)expense.Payments).Clear());

        Assert.Single(expense.AmountHistory);
        Assert.Single(expense.Payments);
    }

    // ---------- Main invariants ----------

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(15)]
    [InlineData(28)]
    public void ChangeAmount_EffectiveMonthIsTheFirstOpenMonth_ForEveryDayOfThreeYears(int closingDay)
    {
        var rule = CardBillingRule.Of(closingDay, closingDay);
        var lastDate = Day(2028, 12, 31);

        for (var today = Day(2026, 1, 1); today <= lastDate; today = today.AddDays(1))
        {
            var expense = NewExpense(start: YM(2025, 1));
            var context = $"closingDay={closingDay}, today={today:yyyy-MM-dd}";

            var effectiveFrom = expense.ChangeAmount(M(1500.00m), today, rule);

            var currentMonth = YM(today.Year, today.Month);
            var expected = today.Day <= closingDay ? currentMonth : currentMonth.AddMonths(1);

            Assert.True(effectiveFrom == expected, $"Wrong effective month: {context}");
            Assert.True(
                effectiveFrom == FixedExpense.FirstOpenMonth(today, rule),
                $"Effective month differs from FirstOpenMonth: {context}");
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(28)]
    public void FrozenMonths_NeverChange_UnderRandomOperations(int closingDay)
    {
        var rule = CardBillingRule.Of(closingDay, closingDay);
        var random = new Random(20261025 + closingDay);
        var start = YM(2026, 1);
        var expense = NewExpense(start: start, amount: 1000.00m);
        var today = Day(2026, 1, 1);

        for (var step = 0; step < 3000; step++)
        {
            today = today.AddDays(random.Next(0, 4));
            var firstOpen = FixedExpense.FirstOpenMonth(today, rule);
            var context = $"closingDay={closingDay}, step={step}, today={today:yyyy-MM-dd}";

            // Snapshot of every month that is already frozen.
            var frozen = new List<(YearMonth Month, bool Active, Money? Amount)>();
            for (var month = start; month < firstOpen; month = month.AddMonths(1))
            {
                frozen.Add((month, expense.IsActiveIn(month), expense.AmountIn(month)));
            }

            try
            {
                switch (random.Next(4))
                {
                    case 0:
                        expense.ChangeAmount(M(random.Next(1, 500000) / 100m), today, rule);
                        break;
                    case 1:
                        expense.End(start.AddMonths(random.Next(0, 80)), today, rule);
                        break;
                    case 2:
                        expense.MarkAsPaid(start.AddMonths(random.Next(0, 80)), today);
                        break;
                    default:
                        expense.RevertPayment(start.AddMonths(random.Next(0, 80)));
                        break;
                }
            }
            catch (InvalidOperationException)
            {
                // A refused operation must leave the frozen months untouched as well.
            }

            foreach (var (month, active, amount) in frozen)
            {
                Assert.True(
                    expense.IsActiveIn(month) == active,
                    $"Frozen month {month.Year}-{month.Month:00} changed its activity: {context}");
                Assert.True(
                    expense.AmountIn(month) == amount,
                    $"Frozen month {month.Year}-{month.Month:00} changed its amount: {context}");
            }

            var history = expense.AmountHistory;
            Assert.True(history[0].EffectiveFrom == start, $"History does not start at the start month: {context}");
            for (var i = 0; i < history.Count; i++)
            {
                Assert.True(history[i].Amount > Money.Zero, $"Non-positive amount in the history: {context}");
                Assert.True(
                    i == 0 || history[i - 1].EffectiveFrom < history[i].EffectiveFrom,
                    $"History is not strictly ascending: {context}");
            }

            foreach (var paidMonth in expense.Payments.Keys)
            {
                Assert.True(
                    expense.IsActiveIn(paidMonth),
                    $"Paid month {paidMonth.Year}-{paidMonth.Month:00} is not active: {context}");
            }
        }
    }

    [Fact]
    public void Payments_FollowASimpleModel_UnderRandomMarkAndRevert()
    {
        var random = new Random(20261026);
        var expense = NewExpense(start: YM(2026, 1), end: YM(2026, 12));
        var model = new Dictionary<YearMonth, DateOnly>();

        for (var step = 0; step < 3000; step++)
        {
            var month = YM(2026, random.Next(1, 13));
            var context = $"step={step}, month={month.Month}";

            if (random.Next(2) == 0)
            {
                var date = Day(2026, 1, 1).AddDays(random.Next(0, 400));
                var expectedChange = !model.ContainsKey(month);

                var changed = expense.MarkAsPaid(month, date);

                if (expectedChange)
                {
                    model[month] = date;
                }

                Assert.True(changed == expectedChange, $"Wrong result of MarkAsPaid: {context}");
            }
            else
            {
                var expectedChange = model.Remove(month);

                var changed = expense.RevertPayment(month);

                Assert.True(changed == expectedChange, $"Wrong result of RevertPayment: {context}");
            }

            Assert.True(
                expense.IsPaid(month) == model.ContainsKey(month),
                $"IsPaid differs from the model: {context}");
            Assert.True(
                expense.PaidOn(month) == (model.TryGetValue(month, out var modelDate) ? (DateOnly?)modelDate : null),
                $"PaidOn differs from the model: {context}");
            Assert.True(
                expense.Payments.Count == model.Count,
                $"Payment count differs from the model: {context}");
        }
    }
}