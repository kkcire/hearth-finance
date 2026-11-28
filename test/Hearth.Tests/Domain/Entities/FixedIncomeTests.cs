using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.Entities;

public sealed class FixedIncomeTests
{
    private static readonly DateOnly DefaultToday = new(2026, 12, 31);

    private static Money M(decimal amount) => Money.FromDecimal(amount);

    private static YearMonth YM(int year, int month) => YearMonth.Of(year, month);

    private static FixedIncome NewFixedIncome(
        YearMonth? startMonth = null,
        YearMonth? endMonth = null,
        int expectedDayOfMonth = 5,
        decimal amount = 5000m,
        string description = "Salário",
        Guid? conditionId = null) =>
        FixedIncome.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            startMonth ?? YM(2026, 10),
            endMonth,
            expectedDayOfMonth,
            M(amount),
            description,
            conditionId);

    [Fact]
    public void Create_StoresGivenValues_TrimsDescription_AndDefaultsConditionToOwn()
    {
        var id = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var income = FixedIncome.Create(
            id, accountId, YM(2026, 10), YM(2027, 3), 5, M(5000m), "  Salário CLT  ");

        Assert.Equal(id, income.Id);
        Assert.Equal(accountId, income.AccountId);
        Assert.Equal(YM(2026, 10), income.StartMonth);
        Assert.Equal(YM(2027, 3), income.EndMonth);
        Assert.Equal(5, income.ExpectedDayOfMonth);
        Assert.Equal("Salário CLT", income.Description);
        Assert.Equal(SystemPurchaseConditions.OwnId, income.ConditionId);
        Assert.True(income.IsOwn);

        var entry = Assert.Single(income.AmountHistory);
        Assert.Equal(YM(2026, 10), entry.EffectiveFrom);
        Assert.Equal(M(5000m), entry.Amount);
        Assert.Empty(income.Receipts);
    }

    [Fact]
    public void Create_WithAccount_EnforcesTrackingStartMonthBoundary()
    {
        var account = Account.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Conta", M(1000m), TestTime.UtcOn(2026, 11, 15));

        Assert.Throws<DomainException>(() => FixedIncome.Create(
            Guid.NewGuid(), account, YM(2026, 10), null, 5, M(5000m), "Antes"));

        var valid = FixedIncome.Create(
            Guid.NewGuid(), account, YM(2026, 11), null, 5, M(5000m), "Válida");

        Assert.Equal(account.Id, valid.AccountId);
        Assert.Equal(YM(2026, 11), valid.StartMonth);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(32)]
    public void Create_InvalidExpectedDayOfMonth_Throws(int invalidDay)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewFixedIncome(expectedDayOfMonth: invalidDay));
    }

    [Fact]
    public void Create_EndMonthBeforeStartMonth_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NewFixedIncome(startMonth: YM(2026, 10), endMonth: YM(2026, 9)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Create_NonPositiveAmount_Throws(decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewFixedIncome(amount: amount));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankDescription_Throws(string? description)
    {
        Assert.ThrowsAny<ArgumentException>(() => NewFixedIncome(description: description!));
    }

    [Fact]
    public void ExpectedDateIn_ClampsDay31ToLastDayOfShorterMonths()
    {
        var income = NewFixedIncome(expectedDayOfMonth: 31);

        Assert.Equal(new DateOnly(2026, 2, 28), income.ExpectedDateIn(YM(2026, 2)));
        Assert.Equal(new DateOnly(2028, 2, 29), income.ExpectedDateIn(YM(2028, 2)));
        Assert.Equal(new DateOnly(2026, 4, 30), income.ExpectedDateIn(YM(2026, 4)));
        Assert.Equal(new DateOnly(2026, 10, 31), income.ExpectedDateIn(YM(2026, 10)));
    }

    [Fact]
    public void ChangeAmountFrom_UpdatesBaselineFromEffectiveMonthOnward_WithoutRewritingPriorHistory()
    {
        var income = NewFixedIncome(startMonth: YM(2026, 1), amount: 5000m);

        income.ChangeAmountFrom(YM(2026, 4), M(5500m));
        income.ChangeAmountFrom(YM(2026, 9), M(6000m));

        Assert.Equal(3, income.AmountHistory.Count);
        Assert.Equal(M(5000m), income.GetExpectedAmountFor(YM(2026, 1)));
        Assert.Equal(M(5000m), income.GetExpectedAmountFor(YM(2026, 3)));
        Assert.Equal(M(5500m), income.GetExpectedAmountFor(YM(2026, 4)));
        Assert.Equal(M(5500m), income.GetExpectedAmountFor(YM(2026, 8)));
        Assert.Equal(M(6000m), income.GetExpectedAmountFor(YM(2026, 9)));
        Assert.Equal(M(6000m), income.GetExpectedAmountFor(YM(2027, 2)));

        // Re-adjusting from Apr replaces Apr and Sep while keeping Jan..Mar intact
        income.ChangeAmountFrom(YM(2026, 4), M(5750m));
        Assert.Equal(2, income.AmountHistory.Count);
        Assert.Equal(M(5000m), income.GetExpectedAmountFor(YM(2026, 3)));
        Assert.Equal(M(5750m), income.GetExpectedAmountFor(YM(2026, 4)));
        Assert.Equal(M(5750m), income.GetExpectedAmountFor(YM(2026, 9)));
    }

    [Fact]
    public void ChangeAmountFrom_BeforeStartOrAfterEnd_ThrowsDomainException()
    {
        var income = NewFixedIncome(startMonth: YM(2026, 5), endMonth: YM(2026, 10), amount: 5000m);

        Assert.Throws<DomainException>(() => income.ChangeAmountFrom(YM(2026, 4), M(5500m)));
        Assert.Throws<DomainException>(() => income.ChangeAmountFrom(YM(2026, 11), M(5500m)));
    }

    [Fact]
    public void GetExpectedAmountFor_InactiveMonth_ThrowsDomainException()
    {
        var income = NewFixedIncome(startMonth: YM(2026, 5), endMonth: YM(2026, 8), amount: 5000m);

        Assert.False(income.IsActiveIn(YM(2026, 4)));
        Assert.True(income.IsActiveIn(YM(2026, 5)));
        Assert.True(income.IsActiveIn(YM(2026, 8)));
        Assert.False(income.IsActiveIn(YM(2026, 9)));

        Assert.Throws<DomainException>(() => income.GetExpectedAmountFor(YM(2026, 4)));
        Assert.Throws<DomainException>(() => income.GetExpectedAmountFor(YM(2026, 9)));
    }

    [Fact]
    public void ConfirmReceipt_SupportsBonusAndDiscount_PreventsDuplicateMonth_AndRevertsCleanly()
    {
        var income = NewFixedIncome(startMonth: YM(2026, 10), amount: 5000m);

        // Oct: received with R$ 450 bonus
        var octReceipt = income.ConfirmReceipt(YM(2026, 10), new DateOnly(2026, 10, 5), M(5450m), DefaultToday);
        Assert.Equal(YM(2026, 10), octReceipt.Month);
        Assert.Equal(new DateOnly(2026, 10, 5), octReceipt.ReceivedOn);
        Assert.Equal(M(5450m), octReceipt.ActualAmount);
        Assert.True(income.IsReceivedIn(YM(2026, 10)));
        Assert.Equal(M(5000m), income.GetExpectedAmountFor(YM(2026, 10))); // baseline untouched

        // Nov: received with R$ 200 discount
        var novReceipt = income.ConfirmReceipt(YM(2026, 11), new DateOnly(2026, 11, 6), M(4800m), DefaultToday);
        Assert.Equal(M(4800m), novReceipt.ActualAmount);

        // Duplicate month rejected
        Assert.Throws<DomainException>(() =>
            income.ConfirmReceipt(YM(2026, 10), new DateOnly(2026, 10, 7), M(5000m), DefaultToday));

        // Revert is idempotent and allows re-confirming with corrected amount
        Assert.True(income.RevertReceipt(YM(2026, 10)));
        Assert.False(income.RevertReceipt(YM(2026, 10)));
        Assert.False(income.IsReceivedIn(YM(2026, 10)));
        Assert.Null(income.GetReceiptFor(YM(2026, 10)));

        var corrected = income.ConfirmReceipt(YM(2026, 10), new DateOnly(2026, 10, 6), M(5300m), DefaultToday);
        Assert.Equal(M(5300m), corrected.ActualAmount);
    }

    [Fact]
    public void ConfirmReceipt_RejectsFutureDateInactiveMonthNonPositiveAmountAndPreTrackingAccountDate()
    {
        var account = Account.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Conta", M(1000m), TestTime.UtcOn(2026, 10, 15));
        var income = FixedIncome.Create(
            Guid.NewGuid(), account, YM(2026, 10), YM(2026, 11), 5, M(5000m), "Salário");
        var today = new DateOnly(2026, 11, 10);

        Assert.Throws<DomainException>(() =>
            income.ConfirmReceipt(account, YM(2026, 11), today.AddDays(1), M(5000m), today));
        Assert.Throws<DomainException>(() =>
            income.ConfirmReceipt(account, YM(2026, 12), today, M(5000m), today));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            income.ConfirmReceipt(account, YM(2026, 11), today, Money.Zero, today));
        Assert.Throws<DomainException>(() =>
            income.ConfirmReceipt(account, YM(2026, 10), new DateOnly(2026, 9, 30), M(5000m), today));
    }

    [Fact]
    public void StopAt_AndReactivate_ProtectConfirmedReceiptsAndSupportResuming()
    {
        var income = NewFixedIncome(startMonth: YM(2026, 10), amount: 5000m);
        income.ConfirmReceipt(YM(2026, 11), new DateOnly(2026, 11, 5), M(5000m), DefaultToday);

        // Cannot stop before a month with a confirmed receipt
        Assert.Throws<DomainException>(() => income.StopAt(YM(2026, 10)));
        Assert.Null(income.EndMonth);

        // Stopping at Nov (inclusive) succeeds
        income.StopAt(YM(2026, 11));
        Assert.Equal(YM(2026, 11), income.EndMonth);
        Assert.False(income.IsActiveIn(YM(2026, 12)));

        // Reactivate clears EndMonth idempotently
        Assert.True(income.Reactivate());
        Assert.False(income.Reactivate());
        Assert.Null(income.EndMonth);
        Assert.True(income.IsActiveIn(YM(2026, 12)));
    }

    [Fact]
    public void UpdateDetails_UpdatesDescriptionDayAndCondition()
    {
        var income = NewFixedIncome();
        var otherCondition = Guid.NewGuid();

        income.UpdateDetails("  Aluguel sala  ", 15, otherCondition);

        Assert.Equal("Aluguel sala", income.Description);
        Assert.Equal(15, income.ExpectedDayOfMonth);
        Assert.Equal(otherCondition, income.ConditionId);
        Assert.False(income.IsOwn);
    }
}
