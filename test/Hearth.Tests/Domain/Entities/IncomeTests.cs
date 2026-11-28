using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.Entities;

public class IncomeTests
{
    private static readonly DateOnly DefaultDate = new(2026, 10, 28);

    private static Money M(decimal amount) => Money.FromDecimal(amount);

    private static Income NewIncome(
        DateOnly? date = null,
        decimal amount = 5000.00m,
        string description = "Salário",
        Guid? conditionId = null) =>
        Income.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            date ?? DefaultDate,
            M(amount),
            description,
            conditionId);

    // ---------- Create ----------

    [Fact]
    public void Create_StoresTheGivenValues_AndTrimsTheDescription()
    {
        var id = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var income = Income.Create(id, accountId, DefaultDate, M(5000.00m), "  Salário  ");

        Assert.Equal(id, income.Id);
        Assert.Equal(accountId, income.AccountId);
        Assert.Equal(DefaultDate, income.Date);
        Assert.Equal(YearMonth.Of(2026, 10), income.Month);
        Assert.Equal(M(5000.00m), income.Amount);
        Assert.Equal("Salário", income.Description);
        Assert.Equal(SystemPurchaseConditions.OwnId, income.ConditionId);
        Assert.True(income.IsOwn);
    }

    [Fact]
    public void Create_WithAccount_EnforcesTrackingStartBoundary()
    {
        var account = Account.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Conta", M(1000m), TestTime.UtcOn(2026, 11, 15));

        Assert.Throws<DomainException>(() => Income.Create(
            Guid.NewGuid(), account, new DateOnly(2026, 10, 31), M(1000m), "Antes"));

        var onStart = Income.Create(
            Guid.NewGuid(), account, new DateOnly(2026, 11, 1), M(1000m), "No início");

        Assert.Equal(account.Id, onStart.AccountId);
        Assert.Equal(new DateOnly(2026, 11, 1), onStart.Date);
    }

    [Fact]
    public void Create_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Income.Create(
            Guid.Empty, Guid.NewGuid(), DefaultDate, M(10m), "x"));
    }

    [Fact]
    public void Create_EmptyAccountId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Income.Create(
            Guid.NewGuid(), Guid.Empty, DefaultDate, M(10m), "x"));
    }

    [Fact]
    public void Create_EmptyConditionId_Throws()
    {
        Assert.Throws<ArgumentException>(() => NewIncome(conditionId: Guid.Empty));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public void Create_ZeroOrNegativeAmount_Throws(double raw)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewIncome(amount: (decimal)raw));
    }

    [Fact]
    public void Create_SmallestPositiveAmount_IsAccepted()
    {
        Assert.Equal(M(0.01m), NewIncome(amount: 0.01m).Amount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_DescriptionIsRequired(string? description)
    {
        Assert.ThrowsAny<ArgumentException>(() => NewIncome(description: description!));
    }

    // ---------- Month ----------

    [Theory]
    [InlineData(2026, 10, 1, 2026, 10)]
    [InlineData(2026, 10, 31, 2026, 10)]
    [InlineData(2026, 12, 31, 2026, 12)]
    [InlineData(2027, 1, 1, 2027, 1)]
    [InlineData(2028, 2, 29, 2028, 2)]
    public void Month_IsTheCalendarMonthOfTheDate(
        int year, int month, int day, int expectedYear, int expectedMonth)
    {
        var income = NewIncome(date: new DateOnly(year, month, day));

        Assert.Equal(YearMonth.Of(expectedYear, expectedMonth), income.Month);
    }

    // ---------- IsReceivedOn ----------

    [Fact]
    public void IsReceivedOn_ReturnsTrueWhenDateIsBeforeOrEqualToReferenceDate_AndFalseWhenFuture()
    {
        var income = NewIncome(date: new DateOnly(2026, 10, 28));

        Assert.False(income.IsReceivedOn(new DateOnly(2026, 10, 27)));
        Assert.True(income.IsReceivedOn(new DateOnly(2026, 10, 28)));
        Assert.True(income.IsReceivedOn(new DateOnly(2026, 10, 29)));
    }

    [Fact]
    public void IsReceivedOn_HandlesMinAndMaxDateBoundaries()
    {
        var minIncome = NewIncome(date: DateOnly.MinValue);
        var maxIncome = NewIncome(date: DateOnly.MaxValue);

        Assert.True(minIncome.IsReceivedOn(DateOnly.MinValue));
        Assert.True(minIncome.IsReceivedOn(DateOnly.MaxValue));
        Assert.False(maxIncome.IsReceivedOn(DateOnly.MaxValue.AddDays(-1)));
        Assert.True(maxIncome.IsReceivedOn(DateOnly.MaxValue));
    }

    // ---------- UpdateDate ----------

    [Fact]
    public void UpdateDate_ChangesTheDateAndMonth_WithoutEditLocksEvenWhenPreviouslyInThePast()
    {
        var today = new DateOnly(2026, 11, 20);
        var income = NewIncome(date: new DateOnly(2026, 10, 28));
        Assert.True(income.IsReceivedOn(today));

        income.UpdateDate(new DateOnly(2026, 12, 5));

        Assert.Equal(new DateOnly(2026, 12, 5), income.Date);
        Assert.Equal(YearMonth.Of(2026, 12), income.Month);
        Assert.False(income.IsReceivedOn(today));
    }

    [Fact]
    public void UpdateDate_WithAccount_EnforcesTrackingStartBoundary()
    {
        var account = Account.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Conta", M(1000m), TestTime.UtcOn(2026, 11, 15));
        var income = Income.Create(Guid.NewGuid(), account, new DateOnly(2026, 11, 10), M(1000m), "Salário");

        Assert.Throws<DomainException>(() => income.UpdateDate(account, new DateOnly(2026, 10, 31)));
        Assert.Equal(new DateOnly(2026, 11, 10), income.Date);

        income.UpdateDate(account, new DateOnly(2026, 11, 1));
        Assert.Equal(new DateOnly(2026, 11, 1), income.Date);
    }

    // ---------- UpdateAmount ----------

    [Fact]
    public void UpdateAmount_ChangesTheAmount_EvenWhenDateIsInThePast()
    {
        var today = new DateOnly(2026, 11, 20);
        var income = NewIncome(date: new DateOnly(2026, 10, 28), amount: 5000.00m);
        Assert.True(income.IsReceivedOn(today));

        income.UpdateAmount(M(5230.45m));

        Assert.Equal(M(5230.45m), income.Amount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void UpdateAmount_ZeroOrNegative_ThrowsAndKeepsTheAmount(double raw)
    {
        var income = NewIncome(amount: 5000.00m);

        Assert.Throws<ArgumentOutOfRangeException>(() => income.UpdateAmount(M((decimal)raw)));

        Assert.Equal(M(5000.00m), income.Amount);
    }

    // ---------- UpdateDescription & UpdateCondition ----------

    [Fact]
    public void UpdateDescription_ChangesAndTrims()
    {
        var income = NewIncome();

        income.UpdateDescription("  Pix da mãe  ");

        Assert.Equal("Pix da mãe", income.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDescription_Empty_ThrowsAndKeepsTheDescription(string? description)
    {
        var income = NewIncome(description: "Salário");

        Assert.ThrowsAny<ArgumentException>(() => income.UpdateDescription(description!));

        Assert.Equal("Salário", income.Description);
    }

    [Fact]
    public void UpdateCondition_ChangesConditionAndIsOwn()
    {
        var income = NewIncome();
        Assert.True(income.IsOwn);

        var otherConditionId = Guid.NewGuid();
        income.UpdateCondition(otherConditionId);

        Assert.Equal(otherConditionId, income.ConditionId);
        Assert.False(income.IsOwn);
        Assert.Throws<ArgumentException>(() => income.UpdateCondition(Guid.Empty));
    }

    // ---------- Independence between fields ----------

    [Fact]
    public void Updates_DoNotAffectTheIdentityOrTheDate()
    {
        var income = NewIncome();
        var id = income.Id;
        var accountId = income.AccountId;

        income.UpdateAmount(M(10.00m));
        income.UpdateDescription("Outra");
        income.UpdateCondition(SystemPurchaseConditions.OthersId);

        Assert.Equal(id, income.Id);
        Assert.Equal(accountId, income.AccountId);
        Assert.Equal(DefaultDate, income.Date);
    }

    // ---------- Main invariants ----------

    [Fact]
    public void MonthAndIsReceivedOn_AlwaysMatchDate_ForEveryDayOfALeapAndANonLeapYear()
    {
        var referenceDate = new DateOnly(2028, 2, 29);
        var lastDate = new DateOnly(2028, 12, 31);

        for (var date = new DateOnly(2027, 1, 1); date <= lastDate; date = date.AddDays(1))
        {
            var income = NewIncome(date: date);

            Assert.True(
                income.Month == YearMonth.Of(date.Year, date.Month),
                $"Month does not match the date {date:yyyy-MM-dd}");
            Assert.True(
                income.IsReceivedOn(referenceDate) == (date <= referenceDate),
                $"IsReceivedOn disagrees with Date <= referenceDate for {date:yyyy-MM-dd}");
        }
    }
}
