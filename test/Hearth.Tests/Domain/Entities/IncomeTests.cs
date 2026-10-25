using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.Entities;

public class IncomeTests
{
    private static readonly DateOnly DefaultExpectedDate = new(2026, 10, 28);

    private static Money M(decimal amount) => Money.FromDecimal(amount);

    private static DateOnly? D(int year, int month, int day) => new DateOnly(year, month, day);

    private static Income NewIncome(
        DateOnly? expectedDate = null,
        decimal amount = 5000.00m,
        string description = "Salário") =>
        Income.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            expectedDate ?? DefaultExpectedDate,
            M(amount),
            description);

    // ---------- Create ----------

    [Fact]
    public void Create_StoresTheGivenValues_AndTrimsTheDescription()
    {
        var id = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var income = Income.Create(id, accountId, DefaultExpectedDate, M(5000.00m), "  Salário  ");

        Assert.Equal(id, income.Id);
        Assert.Equal(accountId, income.AccountId);
        Assert.Equal(DefaultExpectedDate, income.ExpectedDate);
        Assert.Equal(M(5000.00m), income.Amount);
        Assert.Equal("Salário", income.Description);
    }

    [Fact]
    public void Create_AlwaysStartsAsExpected()
    {
        var income = NewIncome();

        Assert.False(income.IsReceived);
        Assert.Null(income.ReceivedOn);
        Assert.Null(income.ReceivedMonth);
    }

    [Fact]
    public void Create_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Income.Create(
            Guid.Empty, Guid.NewGuid(), DefaultExpectedDate, M(10m), "x"));
    }

    [Fact]
    public void Create_EmptyAccountId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Income.Create(
            Guid.NewGuid(), Guid.Empty, DefaultExpectedDate, M(10m), "x"));
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

    // ---------- ExpectedMonth ----------

    [Theory]
    [InlineData(2026, 10, 1, 2026, 10)]
    [InlineData(2026, 10, 31, 2026, 10)]
    [InlineData(2026, 12, 31, 2026, 12)]
    [InlineData(2027, 1, 1, 2027, 1)]
    [InlineData(2028, 2, 29, 2028, 2)]
    public void ExpectedMonth_IsTheCalendarMonthOfTheExpectedDate(
        int year, int month, int day, int expectedYear, int expectedMonth)
    {
        var income = NewIncome(expectedDate: new DateOnly(year, month, day));

        Assert.Equal(YearMonth.Of(expectedYear, expectedMonth), income.ExpectedMonth);
    }

    // ---------- MarkAsReceived ----------

    [Fact]
    public void MarkAsReceived_FromExpected_BecomesReceivedAndReturnsTrue()
    {
        var income = NewIncome();

        var changed = income.MarkAsReceived(new DateOnly(2026, 10, 28));

        Assert.True(changed);
        Assert.True(income.IsReceived);
        Assert.Equal(D(2026, 10, 28), income.ReceivedOn);
        Assert.Equal(YearMonth.Of(2026, 10), income.ReceivedMonth);
    }

    [Fact]
    public void MarkAsReceived_Twice_WithTheSameDate_ReturnsFalseAndKeepsTheState()
    {
        var income = NewIncome();
        income.MarkAsReceived(new DateOnly(2026, 10, 28));

        var changed = income.MarkAsReceived(new DateOnly(2026, 10, 28));

        Assert.False(changed);
        Assert.True(income.IsReceived);
        Assert.Equal(D(2026, 10, 28), income.ReceivedOn);
    }

    [Fact]
    public void MarkAsReceived_Twice_WithAnotherDate_KeepsTheOriginalDate()
    {
        var income = NewIncome();
        income.MarkAsReceived(new DateOnly(2026, 10, 28));

        var changed = income.MarkAsReceived(new DateOnly(2026, 11, 5));

        Assert.False(changed);
        Assert.Equal(D(2026, 10, 28), income.ReceivedOn);
        Assert.Equal(YearMonth.Of(2026, 10), income.ReceivedMonth);
    }

    [Fact]
    public void MarkAsReceived_RepeatedManyTimes_NeverChangesTheFirstDate()
    {
        var income = NewIncome();
        Assert.True(income.MarkAsReceived(new DateOnly(2026, 10, 28)));

        for (var day = 1; day <= 28; day++)
        {
            Assert.False(income.MarkAsReceived(new DateOnly(2026, 12, day)));
        }

        Assert.Equal(D(2026, 10, 28), income.ReceivedOn);
    }

    [Fact]
    public void MarkAsReceived_InAnotherMonthThanExpected_KeepsEachRegimeOnItsOwnMonth()
    {
        // Expected on 28/10, received on 02/11: October by accrual, November by cash.
        var income = NewIncome(expectedDate: new DateOnly(2026, 10, 28));

        income.MarkAsReceived(new DateOnly(2026, 11, 2));

        Assert.Equal(YearMonth.Of(2026, 10), income.ExpectedMonth);
        Assert.Equal(YearMonth.Of(2026, 11), income.ReceivedMonth);
    }

    [Fact]
    public void MarkAsReceived_BeforeTheExpectedDate_IsAccepted()
    {
        var income = NewIncome(expectedDate: new DateOnly(2026, 10, 28));

        var changed = income.MarkAsReceived(new DateOnly(2026, 9, 30));

        Assert.True(changed);
        Assert.Equal(YearMonth.Of(2026, 9), income.ReceivedMonth);
        Assert.Equal(YearMonth.Of(2026, 10), income.ExpectedMonth);
    }

    [Fact]
    public void MarkAsReceived_AcceptsTheLimitsOfTheSupportedDates()
    {
        var first = NewIncome();
        var last = NewIncome();

        first.MarkAsReceived(DateOnly.MinValue);
        last.MarkAsReceived(DateOnly.MaxValue);

        Assert.Equal(YearMonth.Of(1, 1), first.ReceivedMonth);
        Assert.Equal(YearMonth.Of(9999, 12), last.ReceivedMonth);
    }

    // ---------- RevertToExpected ----------

    [Fact]
    public void RevertToExpected_FromReceived_BecomesExpectedAndReturnsTrue()
    {
        var income = NewIncome();
        income.MarkAsReceived(new DateOnly(2026, 10, 28));

        var changed = income.RevertToExpected();

        Assert.True(changed);
        Assert.False(income.IsReceived);
        Assert.Null(income.ReceivedOn);
        Assert.Null(income.ReceivedMonth);
    }

    [Fact]
    public void RevertToExpected_Twice_ReturnsFalseTheSecondTime()
    {
        var income = NewIncome();
        income.MarkAsReceived(new DateOnly(2026, 10, 28));

        Assert.True(income.RevertToExpected());
        Assert.False(income.RevertToExpected());

        Assert.False(income.IsReceived);
    }

    [Fact]
    public void RevertToExpected_WhenNeverReceived_ReturnsFalseAndChangesNothing()
    {
        var income = NewIncome();

        var changed = income.RevertToExpected();

        Assert.False(changed);
        Assert.False(income.IsReceived);
        Assert.Null(income.ReceivedOn);
    }

    [Fact]
    public void AfterReverting_CanBeMarkedAgainWithAnotherDate()
    {
        var income = NewIncome();
        income.MarkAsReceived(new DateOnly(2026, 10, 28));
        income.RevertToExpected();

        var changed = income.MarkAsReceived(new DateOnly(2026, 11, 3));

        Assert.True(changed);
        Assert.Equal(D(2026, 11, 3), income.ReceivedOn);
        Assert.Equal(YearMonth.Of(2026, 11), income.ReceivedMonth);
    }

    // ---------- UpdateAmount ----------

    [Fact]
    public void UpdateAmount_ChangesTheAmount_WhileExpected()
    {
        var income = NewIncome(amount: 5000.00m);

        income.UpdateAmount(M(5230.45m));

        Assert.Equal(M(5230.45m), income.Amount);
    }

    [Fact]
    public void UpdateAmount_ChangesTheAmount_AfterReceived_AndKeepsTheState()
    {
        var income = NewIncome(amount: 5000.00m);
        income.MarkAsReceived(new DateOnly(2026, 10, 28));

        income.UpdateAmount(M(5230.45m));

        Assert.Equal(M(5230.45m), income.Amount);
        Assert.True(income.IsReceived);
        Assert.Equal(D(2026, 10, 28), income.ReceivedOn);
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

    // ---------- UpdateDescription ----------

    [Fact]
    public void UpdateDescription_ChangesAndTrims()
    {
        var income = NewIncome();

        income.UpdateDescription("  Pix da mãe  ");

        Assert.Equal("Pix da mãe", income.Description);
    }

    [Fact]
    public void UpdateDescription_AfterReceived_KeepsTheState()
    {
        var income = NewIncome();
        income.MarkAsReceived(new DateOnly(2026, 10, 28));

        income.UpdateDescription("Salário outubro");

        Assert.Equal("Salário outubro", income.Description);
        Assert.True(income.IsReceived);
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

    // ---------- Independence between fields ----------

    [Fact]
    public void Updates_DoNotAffectTheIdentityOrTheExpectedDate()
    {
        var income = NewIncome();
        var id = income.Id;
        var accountId = income.AccountId;

        income.UpdateAmount(M(10.00m));
        income.UpdateDescription("Outra");
        income.MarkAsReceived(new DateOnly(2026, 11, 2));
        income.RevertToExpected();

        Assert.Equal(id, income.Id);
        Assert.Equal(accountId, income.AccountId);
        Assert.Equal(DefaultExpectedDate, income.ExpectedDate);
    }

    // ---------- Main invariants ----------

    [Fact]
    public void IsReceived_IsTrueExactlyWhenReceivedOnHasAValue_ForARandomSequenceOfOperations()
    {
        var random = new Random(20261025);
        var income = NewIncome();

        // A simple model of what the entity must do, to compare against.
        var modelReceivedOn = (DateOnly?)null;

        for (var step = 0; step < 5000; step++)
        {
            var context = $"step={step}";

            if (random.Next(2) == 0)
            {
                var date = new DateOnly(2026, 1, 1).AddDays(random.Next(0, 730));
                var expectedChange = modelReceivedOn is null;

                var changed = income.MarkAsReceived(date);

                if (expectedChange)
                {
                    modelReceivedOn = date;
                }

                Assert.True(changed == expectedChange, $"Wrong result of MarkAsReceived: {context}");
            }
            else
            {
                var expectedChange = modelReceivedOn is not null;

                var changed = income.RevertToExpected();

                modelReceivedOn = null;

                Assert.True(changed == expectedChange, $"Wrong result of RevertToExpected: {context}");
            }

            Assert.True(
                income.IsReceived == income.ReceivedOn.HasValue,
                $"IsReceived disagrees with ReceivedOn: {context}");
            Assert.True(
                income.ReceivedOn == modelReceivedOn,
                $"ReceivedOn differs from the model: {context}");
            Assert.True(
                income.ReceivedMonth is null == modelReceivedOn is null,
                $"ReceivedMonth presence differs from the model: {context}");
        }
    }

    [Fact]
    public void ReceivedMonth_AlwaysMatchesTheReceivedDate_ForEveryDayOfALeapAndANonLeapYear()
    {
        var lastDate = new DateOnly(2028, 12, 31);

        for (var date = new DateOnly(2027, 1, 1); date <= lastDate; date = date.AddDays(1))
        {
            var income = NewIncome();

            income.MarkAsReceived(date);

            Assert.True(
                income.ReceivedMonth == YearMonth.Of(date.Year, date.Month),
                $"ReceivedMonth does not match the date {date:yyyy-MM-dd}");
        }
    }
}