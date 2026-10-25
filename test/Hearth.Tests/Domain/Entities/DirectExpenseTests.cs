using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.Entities;

public class DirectExpenseTests
{
    private static readonly DateOnly DefaultDate = new(2026, 10, 10);

    private static Money M(decimal amount) => Money.FromDecimal(amount);

    private static DirectExpense NewExpense(
        DateOnly? date = null,
        decimal amount = 45.90m,
        string description = "Mercado",
        PurchaseCategory category = PurchaseCategory.Food) =>
        DirectExpense.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            date ?? DefaultDate,
            M(amount),
            description,
            category);

    // ---------- Create ----------

    [Fact]
    public void Create_StoresTheGivenValues_AndTrimsTheDescription()
    {
        var id = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var expense = DirectExpense.Create(
            id,
            accountId,
            DefaultDate,
            M(45.90m),
            "  Mercado  ",
            PurchaseCategory.Food);

        Assert.Equal(id, expense.Id);
        Assert.Equal(accountId, expense.AccountId);
        Assert.Equal(DefaultDate, expense.Date);
        Assert.Equal(M(45.90m), expense.Amount);
        Assert.Equal("Mercado", expense.Description);
        Assert.Equal(PurchaseCategory.Food, expense.Category);
    }

    [Fact]
    public void Create_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() => DirectExpense.Create(
            Guid.Empty, Guid.NewGuid(), DefaultDate, M(10m), "x", PurchaseCategory.Food));
    }

    [Fact]
    public void Create_EmptyAccountId_Throws()
    {
        Assert.Throws<ArgumentException>(() => DirectExpense.Create(
            Guid.NewGuid(), Guid.Empty, DefaultDate, M(10m), "x", PurchaseCategory.Food));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public void Create_ZeroOrNegativeAmount_Throws(double raw)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewExpense(amount: (decimal)raw));
    }

    [Fact]
    public void Create_SmallestPositiveAmount_IsAccepted()
    {
        var expense = NewExpense(amount: 0.01m);

        Assert.Equal(M(0.01m), expense.Amount);
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
            () => NewExpense(category: (PurchaseCategory)category));
    }

    [Fact]
    public void Create_AcceptsEveryKnownCategory()
    {
        foreach (var category in Enum.GetValues<PurchaseCategory>())
        {
            var expense = NewExpense(category: category);

            Assert.Equal(category, expense.Category);
        }
    }

    [Fact]
    public void Create_AcceptsTheLimitsOfTheSupportedDates()
    {
        var first = NewExpense(date: DateOnly.MinValue);
        var last = NewExpense(date: DateOnly.MaxValue);

        Assert.Equal(YearMonth.Of(1, 1), first.Month);
        Assert.Equal(YearMonth.Of(9999, 12), last.Month);
    }

    // ---------- Month ----------

    [Theory]
    [InlineData(2026, 10, 1, 2026, 10)]
    [InlineData(2026, 10, 31, 2026, 10)]
    [InlineData(2026, 12, 31, 2026, 12)]
    [InlineData(2027, 1, 1, 2027, 1)]
    [InlineData(2028, 2, 29, 2028, 2)]
    public void Month_IsTheCalendarMonthOfTheDate_NoMatterTheClosingDay(
        int year, int month, int day, int expectedYear, int expectedMonth)
    {
        // A direct expense never enters the invoice, so the day 3 closing has no effect on it.
        var expense = NewExpense(date: new DateOnly(year, month, day));

        Assert.Equal(YearMonth.Of(expectedYear, expectedMonth), expense.Month);
    }

    // ---------- UpdateDate ----------

    [Fact]
    public void UpdateDate_ChangesTheDateAndTheMonth()
    {
        var expense = NewExpense();

        expense.UpdateDate(new DateOnly(2027, 1, 5));

        Assert.Equal(new DateOnly(2027, 1, 5), expense.Date);
        Assert.Equal(YearMonth.Of(2027, 1), expense.Month);
    }

    [Fact]
    public void UpdateDate_IsNeverLocked_EvenForPastMonths()
    {
        var expense = NewExpense(date: new DateOnly(2026, 10, 10));

        expense.UpdateDate(new DateOnly(2020, 1, 1));

        Assert.Equal(new DateOnly(2020, 1, 1), expense.Date);
    }

    // ---------- UpdateAmount ----------

    [Fact]
    public void UpdateAmount_Changes()
    {
        var expense = NewExpense(amount: 45.90m);

        expense.UpdateAmount(M(80.00m));

        Assert.Equal(M(80.00m), expense.Amount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void UpdateAmount_ZeroOrNegative_ThrowsAndKeepsTheAmount(double raw)
    {
        var expense = NewExpense(amount: 45.90m);

        Assert.Throws<ArgumentOutOfRangeException>(() => expense.UpdateAmount(M((decimal)raw)));

        Assert.Equal(M(45.90m), expense.Amount);
    }

    // ---------- UpdateDescription ----------

    [Fact]
    public void UpdateDescription_ChangesAndTrims()
    {
        var expense = NewExpense();

        expense.UpdateDescription("  Padaria  ");

        Assert.Equal("Padaria", expense.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDescription_Empty_ThrowsAndKeepsTheDescription(string? description)
    {
        var expense = NewExpense(description: "Mercado");

        Assert.ThrowsAny<ArgumentException>(() => expense.UpdateDescription(description!));

        Assert.Equal("Mercado", expense.Description);
    }

    // ---------- UpdateCategory ----------

    [Fact]
    public void UpdateCategory_Changes()
    {
        var expense = NewExpense(category: PurchaseCategory.Food);

        expense.UpdateCategory(PurchaseCategory.Transport);

        Assert.Equal(PurchaseCategory.Transport, expense.Category);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void UpdateCategory_Unknown_ThrowsAndKeepsTheCategory(int category)
    {
        var expense = NewExpense(category: PurchaseCategory.Food);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => expense.UpdateCategory((PurchaseCategory)category));

        Assert.Equal(PurchaseCategory.Food, expense.Category);
    }

    // ---------- Independence between fields ----------

    [Fact]
    public void Updates_DoNotAffectTheOtherFields()
    {
        var expense = NewExpense();
        var id = expense.Id;
        var accountId = expense.AccountId;

        expense.UpdateAmount(M(10.00m));
        expense.UpdateCategory(PurchaseCategory.Health);
        expense.UpdateDescription("Farmácia");
        expense.UpdateDate(new DateOnly(2026, 11, 20));

        Assert.Equal(id, expense.Id);
        Assert.Equal(accountId, expense.AccountId);
        Assert.Equal(M(10.00m), expense.Amount);
        Assert.Equal(PurchaseCategory.Health, expense.Category);
        Assert.Equal("Farmácia", expense.Description);
        Assert.Equal(new DateOnly(2026, 11, 20), expense.Date);
    }

    // ---------- Main invariant ----------

    [Fact]
    public void Month_AlwaysMatchesTheDate_ForEveryDayOfALeapAndANonLeapYear()
    {
        var expense = NewExpense(date: new DateOnly(2027, 1, 1));
        var lastDate = new DateOnly(2028, 12, 31);

        for (var date = new DateOnly(2027, 1, 1); date <= lastDate; date = date.AddDays(1))
        {
            expense.UpdateDate(date);

            Assert.True(
                expense.Month == YearMonth.Of(date.Year, date.Month),
                $"Month does not match the date {date:yyyy-MM-dd}");
            Assert.True(
                expense.Month.FirstDay <= date && date <= expense.Month.LastDay,
                $"Date {date:yyyy-MM-dd} is outside its own month");
        }
    }
}