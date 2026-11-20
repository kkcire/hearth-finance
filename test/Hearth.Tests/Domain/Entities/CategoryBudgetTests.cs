using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.Entities;

public class CategoryBudgetTests
{
    private static readonly YearMonth DefaultStart = YearMonth.Of(2026, 1);

    private static Money M(decimal amount) => Money.FromDecimal(amount);

    private static YearMonth YM(int year, int month) => YearMonth.Of(year, month);

    private static DateOnly Day(int year, int month, int day) => new(year, month, day);

    private static CategoryBudget NewBudget(
        YearMonth? start = null,
        YearMonth? end = null,
        decimal amount = 400.00m,
        Guid? accountId = null,
        Guid? categoryId = null) =>
        CategoryBudget.Create(
            Guid.NewGuid(),
            accountId ?? Guid.NewGuid(),
            categoryId ?? Guid.NewGuid(),
            start ?? DefaultStart,
            end,
            M(amount));

    [Fact]
    public void Create_StoresValuesAndInitialHistory()
    {
        var id = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var budget = CategoryBudget.Create(id, accountId, categoryId, YM(2026, 10), YM(2027, 3), M(200.00m));

        Assert.Equal(id, budget.Id);
        Assert.Equal(accountId, budget.AccountId);
        Assert.Equal(categoryId, budget.CategoryId);
        Assert.Equal(YM(2026, 10), budget.StartMonth);
        Assert.Equal(YM(2027, 3), budget.EndMonth);

        var entry = Assert.Single(budget.AmountHistory);
        Assert.Equal(YM(2026, 10), entry.EffectiveFrom);
        Assert.Equal(M(200.00m), entry.Amount);
    }

    [Fact]
    public void Create_InvalidArguments_Throw()
    {
        Assert.Throws<ArgumentException>(() =>
            CategoryBudget.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), DefaultStart, null, M(100m)));
        Assert.Throws<ArgumentException>(() =>
            CategoryBudget.Create(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), DefaultStart, null, M(100m)));
        Assert.Throws<ArgumentException>(() =>
            CategoryBudget.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, DefaultStart, null, M(100m)));
        Assert.Throws<ArgumentNullException>(() =>
            CategoryBudget.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null!, null, M(100m)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NewBudget(start: YM(2026, 10), end: YM(2026, 9)));
        Assert.Throws<ArgumentOutOfRangeException>(() => NewBudget(amount: 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => NewBudget(amount: -50m));
    }

    [Fact]
    public void FirstOpenMonth_IsAlwaysCurrentCalendarMonth()
    {
        Assert.Equal(YM(2026, 10), CategoryBudget.FirstOpenMonth(Day(2026, 10, 1)));
        Assert.Equal(YM(2026, 10), CategoryBudget.FirstOpenMonth(Day(2026, 10, 15)));
        Assert.Equal(YM(2026, 10), CategoryBudget.FirstOpenMonth(Day(2026, 10, 31)));
    }

    [Fact]
    public void OverlapsWith_DetectsOverlappingAndDisjointRanges()
    {
        var bounded = NewBudget(start: YM(2026, 3), end: YM(2026, 6));
        var openEnded = NewBudget(start: YM(2026, 7), end: null);

        Assert.False(bounded.OverlapsWith(openEnded));
        Assert.False(openEnded.OverlapsWith(bounded));
        Assert.False(bounded.OverlapsWith(YM(2026, 1), YM(2026, 2)));
        Assert.True(bounded.OverlapsWith(YM(2026, 2), YM(2026, 3)));
        Assert.True(bounded.OverlapsWith(YM(2026, 6), YM(2026, 8)));
        Assert.True(bounded.OverlapsWith(YM(2026, 4), null));
        Assert.True(openEnded.OverlapsWith(YM(2030, 1), null));
    }

    [Fact]
    public void ChangeAmount_AppliesFromCurrentCalendarMonthAndPreservesPastMonths()
    {
        var budget = NewBudget(start: YM(2026, 1), amount: 160.00m);

        var effective = budget.ChangeAmount(M(200.00m), Day(2026, 10, 28));

        Assert.Equal(YM(2026, 10), effective);
        Assert.Equal(M(160.00m), budget.AmountIn(YM(2026, 9)));
        Assert.Equal(M(200.00m), budget.AmountIn(YM(2026, 10)));
        Assert.Equal(M(200.00m), budget.AmountIn(YM(2026, 11)));
    }

    [Fact]
    public void ChangeAmount_BeforeStartMonth_AppliesFromStartMonth()
    {
        var budget = NewBudget(start: YM(2027, 2), amount: 160.00m);

        var effective = budget.ChangeAmount(M(220.00m), Day(2026, 11, 20));

        Assert.Equal(YM(2027, 2), effective);
        var entry = Assert.Single(budget.AmountHistory);
        Assert.Equal(M(220.00m), entry.Amount);
    }

    [Fact]
    public void ChangeAmount_WhenBudgetAlreadyEndedInPast_Throws()
    {
        var budget = NewBudget(start: YM(2026, 1), end: YM(2026, 9));

        Assert.Throws<DomainException>(() => budget.ChangeAmount(M(300.00m), Day(2026, 10, 1)));
    }

    [Theory]
    [InlineData(2026, 10, 15, 2026, 10, true)]  // stops from Nov (open)
    [InlineData(2026, 10, 15, 2026, 9, true)]   // stops from Oct (current month is open)
    [InlineData(2026, 10, 15, 2026, 8, false)]  // would deactivate Sep (past)
    public void End_ProtectsOnlyPastCalendarMonths(
        int todayYear, int todayMonth, int todayDay, int lastYear, int lastMonth, bool allowed)
    {
        var budget = NewBudget(start: YM(2026, 1));
        var today = Day(todayYear, todayMonth, todayDay);

        if (allowed)
        {
            budget.End(YM(lastYear, lastMonth), today);
            Assert.Equal(YM(lastYear, lastMonth), budget.EndMonth);
        }
        else
        {
            Assert.Throws<DomainException>(() => budget.End(YM(lastYear, lastMonth), today));
            Assert.Null(budget.EndMonth);
        }
    }

    [Fact]
    public void End_ExtendingOverPastMonths_Throws_ExtendingOverOpenMonths_Succeeds()
    {
        var endedInPast = NewBudget(start: YM(2026, 1), end: YM(2026, 8));
        Assert.Throws<DomainException>(() => endedInPast.End(YM(2026, 12), Day(2026, 10, 10)));

        var endedInSep = NewBudget(start: YM(2026, 1), end: YM(2026, 9));
        endedInSep.End(YM(2026, 12), Day(2026, 10, 10));
        Assert.Equal(YM(2026, 12), endedInSep.EndMonth);
    }

    [Fact]
    public void PastMonths_NeverChange_UnderRandomOperations()
    {
        var random = new Random(20261121);
        var start = YM(2026, 1);
        var budget = NewBudget(start: start, amount: 500.00m);
        var today = Day(2026, 1, 1);

        for (var step = 0; step < 2000; step++)
        {
            today = today.AddDays(random.Next(0, 5));
            var firstOpen = CategoryBudget.FirstOpenMonth(today);
            var context = $"step={step}, today={today:yyyy-MM-dd}";

            var past = new List<(YearMonth Month, bool Active, Money? Amount)>();
            for (var month = start; month < firstOpen; month = month.AddMonths(1))
            {
                past.Add((month, budget.IsActiveIn(month), budget.AmountIn(month)));
            }

            try
            {
                if (random.Next(2) == 0)
                {
                    budget.ChangeAmount(M(random.Next(1, 500000) / 100m), today);
                }
                else
                {
                    budget.End(start.AddMonths(random.Next(0, 80)), today);
                }
            }
            catch (DomainException)
            {
                // Refused operations must leave past months untouched too.
            }

            foreach (var (month, active, amount) in past)
            {
                Assert.True(budget.IsActiveIn(month) == active, $"Past month activity changed: {context}");
                Assert.True(budget.AmountIn(month) == amount, $"Past month amount changed: {context}");
            }
        }
    }
}
