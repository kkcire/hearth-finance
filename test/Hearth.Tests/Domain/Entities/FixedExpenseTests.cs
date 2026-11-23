using Hearth.Domain;
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

        var initialEntry = Assert.Single(expense.AmountHistory);
        Assert.Equal(YM(2026, 10), initialEntry.EffectiveFrom);
        Assert.Equal(M(1500.00m), initialEntry.Amount);
    }

    [Fact]
    public void Create_SingleMonthRangeAndExactMaxDescriptionLength_AreAccepted()
    {
        var exactDesc = new string('a', DomainValidation.MaxDescriptionLength);
        var expense = NewExpense(start: YM(2026, 10), end: YM(2026, 10), description: $" {exactDesc} ");

        Assert.Equal(YM(2026, 10), expense.EndMonth);
        Assert.Equal(exactDesc, expense.Description);
        Assert.True(expense.IsActiveIn(YM(2026, 10)));
        Assert.False(expense.IsActiveIn(YM(2026, 9)));
        Assert.False(expense.IsActiveIn(YM(2026, 11)));
        Assert.Null(expense.AmountIn(YM(2026, 9)));
        Assert.Null(expense.AmountIn(YM(2026, 11)));
    }

    [Fact]
    public void Create_InvalidArguments_Throw()
    {
        var tooLong = new string('a', DomainValidation.MaxDescriptionLength + 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => NewExpense(start: YM(2026, 10), end: YM(2026, 9)));
        Assert.Throws<ArgumentNullException>(() => FixedExpense.Create(
            Guid.NewGuid(), Guid.NewGuid(), null!, null, M(10m), "x", DefaultCategoryId));
        Assert.Throws<ArgumentException>(() => FixedExpense.Create(
            Guid.Empty, Guid.NewGuid(), DefaultStart, null, M(10m), "x", DefaultCategoryId));
        Assert.Throws<ArgumentException>(() => FixedExpense.Create(
            Guid.NewGuid(), Guid.Empty, DefaultStart, null, M(10m), "x", DefaultCategoryId));
        Assert.Throws<ArgumentOutOfRangeException>(() => NewExpense(amount: 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => NewExpense(amount: -10m));
        Assert.ThrowsAny<ArgumentException>(() => NewExpense(description: null!));
        Assert.ThrowsAny<ArgumentException>(() => NewExpense(description: "   "));
        Assert.Throws<ArgumentException>(() => NewExpense(description: tooLong));
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
    public void IsActiveIn_AndAmountIn_RespectRangeAndNullGuards()
    {
        var expense = NewExpense(start: YM(2026, 3), end: YM(2026, 5), amount: 200m);

        Assert.False(expense.IsActiveIn(YM(2026, 2)));
        Assert.True(expense.IsActiveIn(YM(2026, 3)));
        Assert.True(expense.IsActiveIn(YM(2026, 4)));
        Assert.True(expense.IsActiveIn(YM(2026, 5)));
        Assert.False(expense.IsActiveIn(YM(2026, 6)));
        Assert.Null(expense.AmountIn(YM(2026, 2)));
        Assert.Null(expense.AmountIn(YM(2026, 6)));

        Assert.Throws<ArgumentNullException>(() => expense.IsActiveIn(null!));
        Assert.Throws<ArgumentNullException>(() => expense.AmountIn(null!));
    }

    [Fact]
    public void ChangeAmount_AppliesFromCurrentCalendarMonthOverwritesSameOpenMonthAndPreservesPastMonths()
    {
        var expense = NewExpense(start: YM(2026, 1), amount: 1000.00m);

        var first = expense.ChangeAmount(M(1100.00m), Day(2026, 3, 10));
        var overwrite = expense.ChangeAmount(M(1150.00m), Day(2026, 3, 28));
        var second = expense.ChangeAmount(M(1200.00m), Day(2026, 6, 15));

        Assert.Equal(YM(2026, 3), first);
        Assert.Equal(YM(2026, 3), overwrite);
        Assert.Equal(YM(2026, 6), second);
        Assert.Equal(3, expense.AmountHistory.Count);
        Assert.Equal(Amt(1000.00m), expense.AmountIn(YM(2026, 2)));
        Assert.Equal(Amt(1150.00m), expense.AmountIn(YM(2026, 3)));
        Assert.Equal(Amt(1150.00m), expense.AmountIn(YM(2026, 5)));
        Assert.Equal(Amt(1200.00m), expense.AmountIn(YM(2026, 6)));
        Assert.Equal(Amt(1200.00m), expense.AmountIn(YM(2027, 1)));
    }

    [Fact]
    public void ChangeAmount_AllowsExactEndMonthAndPastPaidMonths_RejectsNonPositiveAndFuturePaidMonths()
    {
        var exactEnd = NewExpense(start: YM(2026, 1), end: YM(2026, 10), amount: 500m);
        exactEnd.MarkAsPaid(YM(2026, 9), Day(2026, 9, 5), Day(2026, 10, 15));

        // Past month (Sep) is paid, open month (Oct == EndMonth) is unpaid -> allowed
        Assert.Equal(YM(2026, 10), exactEnd.ChangeAmount(M(600m), Day(2026, 10, 15)));
        Assert.Equal(Amt(500m), exactEnd.AmountIn(YM(2026, 9)));
        Assert.Equal(Amt(600m), exactEnd.AmountIn(YM(2026, 10)));

        Assert.Throws<ArgumentOutOfRangeException>(() => exactEnd.ChangeAmount(Money.Zero, Day(2026, 10, 15)));
        Assert.Throws<ArgumentOutOfRangeException>(() => exactEnd.ChangeAmount(M(-1m), Day(2026, 10, 15)));

        // Future paid month blocks change and preserves history
        var futurePaid = NewExpense(start: YM(2026, 1), amount: 500m);
        futurePaid.MarkAsPaid(YM(2026, 12), Day(2026, 10, 5), Day(2026, 10, 20));
        Assert.Throws<DomainException>(() => futurePaid.ChangeAmount(M(700m), Day(2026, 10, 20)));
        Assert.Equal(Amt(500m), futurePaid.AmountIn(YM(2026, 10)));
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
    public void ChangeAmount_WhenEndedInPastOrAffectedMonthIsPaid_ThrowsDomainExceptionAndPreservesState()
    {
        var ended = NewExpense(start: YM(2026, 1), end: YM(2026, 9), amount: 1000m);
        Assert.Throws<DomainException>(() => ended.ChangeAmount(M(1500m), Day(2026, 10, 1)));
        Assert.Equal(Amt(1000m), ended.AmountIn(YM(2026, 9)));

        var paid = NewExpense(start: YM(2026, 1), amount: 1000m);
        paid.MarkAsPaid(YM(2026, 10), Day(2026, 10, 5), Day(2026, 10, 20));
        Assert.Throws<DomainException>(() => paid.ChangeAmount(M(1500m), Day(2026, 10, 20)));
        Assert.Equal(Amt(1000m), paid.AmountIn(YM(2026, 10)));
    }

    [Fact]
    public void End_AllowsIdempotenceAndExactPaidMonth_BlocksFrozenChangesAndPastExtensions()
    {
        var expense = NewExpense(start: YM(2026, 1));
        var today = Day(2026, 10, 25);

        Assert.Throws<ArgumentNullException>(() => expense.End(null!, today));
        Assert.Throws<ArgumentOutOfRangeException>(() => expense.End(YM(2025, 12), today));

        // Ending in September stops October (current month, still open) -> allowed
        expense.End(YM(2026, 9), today);
        Assert.Equal(YM(2026, 9), expense.EndMonth);

        // Extending from September to December only re-opens October onward -> allowed
        expense.End(YM(2026, 12), today);
        Assert.Equal(YM(2026, 12), expense.EndMonth);

        // Ending in August would deactivate September (past calendar month, frozen) -> throws & keeps Dec
        Assert.Throws<DomainException>(() => expense.End(YM(2026, 8), today));
        Assert.Equal(YM(2026, 12), expense.EndMonth);

        // Idempotent End on already-frozen EndMonth does not throw
        var endedFrozen = NewExpense(start: YM(2026, 1), end: YM(2026, 5));
        endedFrozen.End(YM(2026, 5), today);
        Assert.Equal(YM(2026, 5), endedFrozen.EndMonth);

        // Extending a frozen ended expense throws
        Assert.Throws<DomainException>(() => endedFrozen.End(YM(2026, 12), today));

        // Ending exactly on the last paid month is allowed (months AFTER lastMonth are unpaid)
        var paidInOct = NewExpense(start: YM(2026, 1));
        paidInOct.MarkAsPaid(YM(2026, 10), Day(2026, 10, 5), Day(2026, 10, 15));
        paidInOct.End(YM(2026, 10), Day(2026, 10, 15));
        Assert.Equal(YM(2026, 10), paidInOct.EndMonth);

        // MaxYear boundary does not overflow
        var maxBoundary = NewExpense(start: YM(2026, 1));
        maxBoundary.End(YM(YearMonth.MaxYear, 12), today);
        Assert.Equal(YM(YearMonth.MaxYear, 12), maxBoundary.EndMonth);
    }

    [Fact]
    public void End_WhenMonthAfterNewEndIsAlreadyPaid_ThrowsDomainExceptionAndKeepsEndMonth()
    {
        var expense = NewExpense(start: YM(2026, 1));
        expense.MarkAsPaid(YM(2026, 11), Day(2026, 10, 15), Day(2026, 10, 15));

        Assert.Throws<DomainException>(() => expense.End(YM(2026, 10), Day(2026, 10, 15)));
        Assert.Null(expense.EndMonth);
    }

    [Fact]
    public void MarkAsPaid_RevertPayment_IsPaid_PaidOn_AndPaymentsDictionary()
    {
        var expense = NewExpense(start: YM(2026, 10), end: YM(2026, 12));
        var today = Day(2026, 10, 10);

        Assert.False(expense.IsPaid(YM(2026, 10)));
        Assert.Null(expense.PaidOn(YM(2026, 10)));
        Assert.Empty(expense.Payments);

        Assert.True(expense.MarkAsPaid(YM(2026, 10), Day(2026, 10, 5), today));
        Assert.True(expense.IsPaid(YM(2026, 10)));
        Assert.False(expense.MarkAsPaid(YM(2026, 10), Day(2026, 10, 9), today));
        Assert.Equal(Day(2026, 10, 5), expense.PaidOn(YM(2026, 10)));
        Assert.Equal(Day(2026, 10, 5), expense.Payments[YM(2026, 10)]);

        Assert.True(expense.RevertPayment(YM(2026, 10)));
        Assert.False(expense.RevertPayment(YM(2026, 10)));
        Assert.False(expense.IsPaid(YM(2026, 10)));
        Assert.Null(expense.PaidOn(YM(2026, 10)));

        Assert.Throws<DomainException>(() => expense.MarkAsPaid(YM(2026, 9), Day(2026, 9, 5), today));
        Assert.Throws<DomainException>(() => expense.MarkAsPaid(YM(2027, 1), Day(2026, 10, 5), today));
        Assert.Throws<ArgumentNullException>(() => expense.MarkAsPaid(null!, Day(2026, 10, 5), today));
        Assert.Throws<ArgumentNullException>(() => expense.RevertPayment(null!));
        Assert.Throws<ArgumentNullException>(() => expense.IsPaid(null!));
        Assert.Throws<ArgumentNullException>(() => expense.PaidOn(null!));
    }

    [Fact]
    public void MarkAsPaid_FutureDate_ThrowsDomainException_WhileAnticipatedFutureMonthPaidTodaySucceeds()
    {
        var expense = NewExpense(start: YM(2026, 10), end: YM(2026, 12));
        var today = Day(2026, 10, 15);

        Assert.Throws<DomainException>(() => expense.MarkAsPaid(YM(2026, 10), Day(2026, 10, 16), today));
        Assert.False(expense.IsPaid(YM(2026, 10)));

        // Paid exactly on today for a future competence month (anticipation) is valid!
        Assert.True(expense.MarkAsPaid(YM(2026, 12), today, today));
        Assert.Equal(today, expense.PaidOn(YM(2026, 12)));
    }

    [Fact]
    public void UpdateDescription_AndUpdateCategory_ValidateAndPreserveOnFailure()
    {
        var originalCategory = Guid.NewGuid();
        var nextCategory = Guid.NewGuid();
        var expense = NewExpense(description: "Internet", categoryId: originalCategory);

        expense.UpdateDescription("  Fibra Ótica  ");
        expense.UpdateCategory(nextCategory);
        Assert.Equal("Fibra Ótica", expense.Description);
        Assert.Equal(nextCategory, expense.CategoryId);

        Assert.ThrowsAny<ArgumentException>(() => expense.UpdateDescription("   "));
        Assert.Throws<ArgumentException>(() => expense.UpdateCategory(Guid.Empty));
        Assert.Equal("Fibra Ótica", expense.Description);
        Assert.Equal(nextCategory, expense.CategoryId);
    }
}
