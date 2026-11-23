using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.Entities;

public class FixedExpenseTests
{
    private static readonly YearMonth DefaultStart = YearMonth.Of(2026, 1);
    private static readonly Guid DefaultCategoryId = Guid.NewGuid();

    private static Money M(decimal amount) => Money.FromDecimal(amount);

    private static Money? Amt(decimal amount) => M(amount);

    private static YearMonth YM(int year, int month) => YearMonth.Of(year, month);

    private static DateOnly Day(int year, int month, int day) => new(year, month, day);

    private static FixedExpense NewExpense(
        YearMonth? start = null,
        YearMonth? end = null,
        decimal amount = 1000.00m,
        string description = "Aluguel",
        Guid? categoryId = null) =>
        FixedExpense.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            start ?? DefaultStart,
            end,
            M(amount),
            description,
            categoryId ?? DefaultCategoryId);

    [Fact]
    public void Create_StoresTheGivenValues_AndTrimsTheDescription()
    {
        var id = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var expense = FixedExpense.Create(
            id, accountId, YM(2026, 10), YM(2027, 3), M(1500.00m), "  Aluguel  ", categoryId);

        Assert.Equal(id, expense.Id);
        Assert.Equal(accountId, expense.AccountId);
        Assert.Equal(YM(2026, 10), expense.StartMonth);
        Assert.Equal(YM(2027, 3), expense.EndMonth);
        Assert.Equal("Aluguel", expense.Description);
        Assert.Equal(categoryId, expense.CategoryId);
    }

    [Fact]
    public void Create_InvalidArguments_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewExpense(start: YM(2026, 10), end: YM(2026, 9)));
        Assert.Throws<ArgumentNullException>(() => FixedExpense.Create(
            Guid.NewGuid(), Guid.NewGuid(), null!, null, M(10m), "x", DefaultCategoryId));
        Assert.Throws<ArgumentException>(() => FixedExpense.Create(
            Guid.Empty, Guid.NewGuid(), DefaultStart, null, M(10m), "x", DefaultCategoryId));
        Assert.Throws<ArgumentException>(() => FixedExpense.Create(
            Guid.NewGuid(), Guid.Empty, DefaultStart, null, M(10m), "x", DefaultCategoryId));
        Assert.Throws<ArgumentOutOfRangeException>(() => NewExpense(amount: 0m));
        Assert.ThrowsAny<ArgumentException>(() => NewExpense(description: "   "));
        Assert.Throws<ArgumentException>(() => NewExpense(categoryId: Guid.Empty));
    }

    [Theory]
    [InlineData(2026, 10, 1, 2026, 10)]
    [InlineData(2026, 10, 15, 2026, 10)]
    [InlineData(2026, 10, 31, 2026, 10)]
    [InlineData(2026, 12, 31, 2026, 12)]
    public void FirstOpenMonth_AlwaysEqualsCurrentCalendarMonth(
        int year, int month, int day, int expectedYear, int expectedMonth)
    {
        Assert.Equal(YM(expectedYear, expectedMonth), FixedExpense.FirstOpenMonth(Day(year, month, day)));
    }

    [Fact]
    public void ChangeAmount_AppliesFromCurrentCalendarMonthAndPreservesPastMonths()
    {
        var expense = NewExpense(start: YM(2026, 1), amount: 1000.00m);

        var first = expense.ChangeAmount(M(1100.00m), Day(2026, 3, 28));
        var second = expense.ChangeAmount(M(1200.00m), Day(2026, 6, 15));

        Assert.Equal(YM(2026, 3), first);
        Assert.Equal(YM(2026, 6), second);
        Assert.Equal(Amt(1000.00m), expense.AmountIn(YM(2026, 2)));
        Assert.Equal(Amt(1100.00m), expense.AmountIn(YM(2026, 3)));
        Assert.Equal(Amt(1100.00m), expense.AmountIn(YM(2026, 5)));
        Assert.Equal(Amt(1200.00m), expense.AmountIn(YM(2026, 6)));
        Assert.Equal(Amt(1200.00m), expense.AmountIn(YM(2027, 1)));
    }

    [Fact]
    public void ChangeAmount_BeforeStartMonth_AppliesFromStartMonth()
    {
        var expense = NewExpense(start: YM(2027, 3), amount: 1000.00m);

        var effective = expense.ChangeAmount(M(1500.00m), Day(2026, 10, 10));

        Assert.Equal(YM(2027, 3), effective);
        Assert.Single(expense.AmountHistory);
        Assert.Equal(Amt(1500.00m), expense.AmountIn(YM(2027, 3)));
    }

    [Fact]
    public void ChangeAmount_WhenEndedInPastOrAffectedMonthIsPaid_ThrowsDomainException()
    {
        var ended = NewExpense(start: YM(2026, 1), end: YM(2026, 9));
        Assert.Throws<DomainException>(() => ended.ChangeAmount(M(1500m), Day(2026, 10, 1)));

        var paid = NewExpense(start: YM(2026, 1));
        paid.MarkAsPaid(YM(2026, 10), Day(2026, 10, 5));
        Assert.Throws<DomainException>(() => paid.ChangeAmount(M(1500m), Day(2026, 10, 20)));
    }

    [Fact]
    public void End_AllowsEndingFromPreviousMonthWhenCurrentMonthIsStillOpen_BlocksPastFrozenChanges()
    {
        var expense = NewExpense(start: YM(2026, 1));
        var today = Day(2026, 10, 25);

        // Ending in September stops October (current month, still open) -> allowed
        expense.End(YM(2026, 9), today);
        Assert.Equal(YM(2026, 9), expense.EndMonth);

        // Extending from September to December only re-opens October onward -> allowed
        expense.End(YM(2026, 12), today);
        Assert.Equal(YM(2026, 12), expense.EndMonth);

        // Ending in August would deactivate September (past calendar month, frozen) -> throws
        Assert.Throws<DomainException>(() => expense.End(YM(2026, 8), today));
    }

    [Fact]
    public void End_WhenMonthAfterNewEndIsAlreadyPaid_ThrowsDomainException()
    {
        var expense = NewExpense(start: YM(2026, 1));
        expense.MarkAsPaid(YM(2026, 11), Day(2026, 11, 5));

        Assert.Throws<DomainException>(() => expense.End(YM(2026, 10), Day(2026, 10, 15)));
    }

    [Fact]
    public void MarkAsPaid_AndRevertPayment_AreIdempotent()
    {
        var expense = NewExpense(start: YM(2026, 10), end: YM(2026, 12));

        Assert.True(expense.MarkAsPaid(YM(2026, 10), Day(2026, 10, 5)));
        Assert.False(expense.MarkAsPaid(YM(2026, 10), Day(2026, 10, 9)));
        Assert.Equal(Day(2026, 10, 5), expense.PaidOn(YM(2026, 10)));

        Assert.True(expense.RevertPayment(YM(2026, 10)));
        Assert.False(expense.RevertPayment(YM(2026, 10)));
        Assert.False(expense.IsPaid(YM(2026, 10)));

        Assert.Throws<DomainException>(() => expense.MarkAsPaid(YM(2026, 9), Day(2026, 9, 5)));
    }
}
