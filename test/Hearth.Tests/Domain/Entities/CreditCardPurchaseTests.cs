using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.Entities;

public class CreditCardPurchaseTests
{
    private static readonly CardBillingRule Rule3 = CardBillingRule.Of(3);
    private static readonly DateOnly DefaultDate = new(2026, 10, 10);

    private static Money M(decimal amount) => Money.FromDecimal(amount);

    private static CreditCardPurchase NewPurchase(
        DateOnly? date = null,
        decimal amount = 100.00m,
        int count = 3,
        int initial = 1,
        CardBillingRule? rule = null,
        PurchaseCondition? condition = null,
        string description = "Notebook",
        PurchaseCategory category = PurchaseCategory.Shopping) =>
        CreditCardPurchase.Create(
            Guid.NewGuid(),
            date ?? DefaultDate,
            M(amount),
            count,
            initial,
            rule ?? Rule3,
            condition ?? PurchaseCondition.Own,
            description,
            category);

    // ---------- Create ----------

    [Fact]
    public void Create_StoresTheGivenValues_AndTrimsTheDescription()
    {
        var id = Guid.NewGuid();

        var purchase = CreditCardPurchase.Create(
            id,
            DefaultDate,
            M(250.00m),
            5,
            2,
            Rule3,
            PurchaseCondition.Others,
            "  Tênis  ",
            PurchaseCategory.Personal);

        Assert.Equal(id, purchase.Id);
        Assert.Equal(DefaultDate, purchase.Date);
        Assert.Equal(M(250.00m), purchase.Amount);
        Assert.Equal(5, purchase.InstallmentCount);
        Assert.Equal(2, purchase.InitialInstallment);
        Assert.Equal(Rule3, purchase.Rule);
        Assert.Equal(PurchaseCondition.Others, purchase.Condition);
        Assert.Equal("Tênis", purchase.Description);
        Assert.Equal(PurchaseCategory.Personal, purchase.Category);
    }

    [Fact]
    public void Create_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() => CreditCardPurchase.Create(
            Guid.Empty, DefaultDate, M(100m), 3, 1, Rule3, PurchaseCondition.Own, "x", PurchaseCategory.Shopping));
    }

    [Fact]
    public void Create_NullRule_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CreditCardPurchase.Create(
            Guid.NewGuid(), DefaultDate, M(100m), 3, 1, null!, PurchaseCondition.Own, "x", PurchaseCategory.Shopping));
    }

    [Fact]
    public void Create_NullCondition_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CreditCardPurchase.Create(
            Guid.NewGuid(), DefaultDate, M(100m), 3, 1, Rule3, null!, "x", PurchaseCategory.Shopping));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_InstallmentCountBelowOne_Throws(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewPurchase(count: count, initial: 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(4)]
    public void Create_InitialInstallmentOutsideOneToCount_Throws(int initial)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewPurchase(count: 3, initial: initial));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Create_InitialInstallmentFromOneToCount_IsAccepted(int initial)
    {
        var purchase = NewPurchase(count: 3, initial: initial);

        Assert.Equal(initial, purchase.InitialInstallment);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public void Create_ZeroOrNegativeAmount_Throws(double raw)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewPurchase(amount: (decimal)raw));
    }

    [Fact]
    public void Create_AmountTooSmallForEachInstallmentToGetOneCent_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => NewPurchase(amount: 0.02m, count: 3));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_DescriptionIsRequired(string? description)
    {
        Assert.ThrowsAny<ArgumentException>(() => NewPurchase(description: description!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(99)]
    public void Create_UnknownCategory_Throws(int category)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewPurchase(category: (PurchaseCategory)category));
    }

    [Fact]
    public void Create_AcceptsEveryKnownCategory()
    {
        foreach (var category in Enum.GetValues<PurchaseCategory>())
        {
            var purchase = NewPurchase(category: category);

            Assert.Equal(category, purchase.Category);
        }
    }

    [Fact]
    public void Create_DateWhoseCycleIsOutsideTheSupportedYears_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewPurchase(date: new DateOnly(9999, 12, 31)));
    }

    [Fact]
    public void Create_InstallmentsThatReachBeyondTheSupportedYears_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NewPurchase(date: new DateOnly(9999, 11, 10), count: 3));
    }

    // ---------- FirstCycle ----------

    [Theory]
    [InlineData(2026, 10, 2, 2026, 10)]
    [InlineData(2026, 10, 3, 2026, 11)]
    [InlineData(2026, 12, 31, 2027, 1)]
    public void FirstCycle_FollowsThePurchaseDate(
        int year, int month, int day, int closingYear, int closingMonth)
    {
        var purchase = NewPurchase(date: new DateOnly(year, month, day));

        Assert.Equal(YearMonth.Of(closingYear, closingMonth), purchase.FirstCycle.ClosingMonth);
    }

    // ---------- GetInstallments ----------

    [Fact]
    public void GetInstallments_NewPurchase_SplitsTheAmountAndAssignsConsecutiveCycles()
    {
        // Purchase on 10/10/2026 with closing day 3: first installment on the November invoice.
        var installments = NewPurchase().GetInstallments();

        Assert.Equal(3, installments.Count);
        Assert.Equal(new[] { 1, 2, 3 }, installments.Select(i => i.Number));
        Assert.Equal(
            new[] { M(33.33m), M(33.33m), M(33.34m) },
            installments.Select(i => i.Amount));
        Assert.Equal(
            new[] { YearMonth.Of(2026, 11), YearMonth.Of(2026, 12), YearMonth.Of(2027, 1) },
            installments.Select(i => i.Cycle.ClosingMonth));
        Assert.All(installments, i => Assert.False(i.IsPreviouslyPaid));
    }

    [Fact]
    public void GetInstallments_PurchaseBeforeTheClosingDay_StartsOnTheSameMonthInvoice()
    {
        var installments = NewPurchase(date: new DateOnly(2026, 10, 2)).GetInstallments();

        Assert.Equal(
            new[] { YearMonth.Of(2026, 10), YearMonth.Of(2026, 11), YearMonth.Of(2026, 12) },
            installments.Select(i => i.Cycle.ClosingMonth));
    }

    [Fact]
    public void GetInstallments_RollsOverTheYear()
    {
        var installments = NewPurchase(date: new DateOnly(2026, 12, 15), count: 2).GetInstallments();

        Assert.Equal(
            new[] { YearMonth.Of(2027, 1), YearMonth.Of(2027, 2) },
            installments.Select(i => i.Cycle.ClosingMonth));
    }

    [Fact]
    public void GetInstallments_SingleInstallment_IsTheWholeAmount()
    {
        var installments = NewPurchase(amount: 59.90m, count: 1).GetInstallments();

        Assert.Single(installments);
        Assert.Equal(M(59.90m), installments[0].Amount);
        Assert.Equal(1, installments[0].Number);
    }

    [Fact]
    public void GetInstallments_PurchaseInProgress_MarksTheEarlierInstallmentsAsPreviouslyPaid()
    {
        // Original purchase on 10/07/2026, 6 installments, now at installment 4.
        var purchase = NewPurchase(date: new DateOnly(2026, 7, 10), amount: 600.00m, count: 6, initial: 4);

        var installments = purchase.GetInstallments();

        Assert.Equal(
            new[] { true, true, true, false, false, false },
            installments.Select(i => i.IsPreviouslyPaid));
        Assert.Equal(
            new[]
            {
                YearMonth.Of(2026, 8), YearMonth.Of(2026, 9), YearMonth.Of(2026, 10),
                YearMonth.Of(2026, 11), YearMonth.Of(2026, 12), YearMonth.Of(2027, 1),
            },
            installments.Select(i => i.Cycle.ClosingMonth));
        Assert.All(installments, i => Assert.Equal(M(100.00m), i.Amount));
    }

    [Fact]
    public void GetInstallments_InitialInstallmentIsTheLast_OnlyTheLastCounts()
    {
        var installments = NewPurchase(date: new DateOnly(2026, 1, 10), count: 12, initial: 12)
            .GetInstallments();

        Assert.Equal(11, installments.Count(i => i.IsPreviouslyPaid));
        Assert.False(installments[^1].IsPreviouslyPaid);
        Assert.Equal(12, installments[^1].Number);
    }

    [Fact]
    public void GetInstallments_ConditionDoesNotChangeTheCalculation()
    {
        var own = NewPurchase(condition: PurchaseCondition.Own).GetInstallments();
        var others = NewPurchase(condition: PurchaseCondition.Others).GetInstallments();
        var custom = NewPurchase(condition: PurchaseCondition.Custom("Ana")).GetInstallments();

        Assert.Equal(own, others);
        Assert.Equal(own, custom);
    }

    [Fact]
    public void GetInstallments_CalledTwice_ReturnsTheSameValues()
    {
        var purchase = NewPurchase(count: 5, initial: 2);

        Assert.Equal(purchase.GetInstallments(), purchase.GetInstallments());
    }

    // ---------- IsLockedAt ----------

    [Theory]
    [InlineData(2025, 1, 1, false)]
    [InlineData(2026, 10, 10, false)]
    [InlineData(2026, 11, 2, false)]
    [InlineData(2026, 11, 3, true)]
    [InlineData(2026, 12, 1, true)]
    public void IsLockedAt_PurchaseOnTheDefaultDate_LocksOnTheClosingDateOfTheFirstInvoice(
        int year, int month, int day, bool expected)
    {
        // First invoice closes on 03/11/2026.
        var purchase = NewPurchase();

        Assert.Equal(expected, purchase.IsLockedAt(new DateOnly(year, month, day)));
    }

    [Theory]
    [InlineData(2026, 10, 2, false)]
    [InlineData(2026, 10, 3, true)]
    public void IsLockedAt_PurchaseBeforeTheClosingDay_LocksOnTheSameMonthClosingDate(
        int year, int month, int day, bool expected)
    {
        var purchase = NewPurchase(date: new DateOnly(2026, 10, 2));

        Assert.Equal(expected, purchase.IsLockedAt(new DateOnly(year, month, day)));
    }

    [Theory]
    [InlineData(2026, 10, 20, false)]
    [InlineData(2026, 11, 2, false)]
    [InlineData(2026, 11, 3, true)]
    public void IsLockedAt_PurchaseInProgress_UsesTheFirstInstallmentThatStillCounts(
        int year, int month, int day, bool expected)
    {
        // Installments 1 to 3 closed long ago, but the first one that counts (4) closes on 03/11/2026.
        var purchase = NewPurchase(date: new DateOnly(2026, 7, 10), amount: 600.00m, count: 6, initial: 4);

        Assert.Equal(expected, purchase.IsLockedAt(new DateOnly(year, month, day)));
    }

    // ---------- UpdateAmount ----------

    [Fact]
    public void UpdateAmount_BeforeTheClosing_ChangesTheAmountAndTheInstallments()
    {
        var purchase = NewPurchase();

        purchase.UpdateAmount(M(250.00m), new DateOnly(2026, 10, 20));

        Assert.Equal(M(250.00m), purchase.Amount);
        Assert.Equal(
            new[] { M(83.33m), M(83.33m), M(83.34m) },
            purchase.GetInstallments().Select(i => i.Amount));
    }

    [Fact]
    public void UpdateAmount_AfterTheClosing_ThrowsAndKeepsTheAmount()
    {
        var purchase = NewPurchase();

        Assert.Throws<InvalidOperationException>(
            () => purchase.UpdateAmount(M(250.00m), new DateOnly(2026, 11, 3)));

        Assert.Equal(M(100.00m), purchase.Amount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void UpdateAmount_ZeroOrNegative_ThrowsAndKeepsTheAmount(double raw)
    {
        var purchase = NewPurchase();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => purchase.UpdateAmount(M((decimal)raw), new DateOnly(2026, 10, 20)));

        Assert.Equal(M(100.00m), purchase.Amount);
    }

    [Fact]
    public void UpdateAmount_TooSmallToSplit_ThrowsAndKeepsTheAmount()
    {
        var purchase = NewPurchase();

        Assert.Throws<InvalidOperationException>(
            () => purchase.UpdateAmount(M(0.02m), new DateOnly(2026, 10, 20)));

        Assert.Equal(M(100.00m), purchase.Amount);
    }

    // ---------- UpdateDate ----------

    [Fact]
    public void UpdateDate_BeforeTheClosing_ChangesTheDateAndTheFirstCycle()
    {
        var purchase = NewPurchase();

        purchase.UpdateDate(new DateOnly(2026, 11, 5), new DateOnly(2026, 10, 20));

        Assert.Equal(new DateOnly(2026, 11, 5), purchase.Date);
        Assert.Equal(YearMonth.Of(2026, 12), purchase.FirstCycle.ClosingMonth);
    }

    [Fact]
    public void UpdateDate_AfterTheClosing_ThrowsAndKeepsTheDate()
    {
        var purchase = NewPurchase();

        Assert.Throws<InvalidOperationException>(
            () => purchase.UpdateDate(new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 3)));

        Assert.Equal(DefaultDate, purchase.Date);
    }

    [Fact]
    public void UpdateDate_CycleOutsideTheSupportedYears_ThrowsAndKeepsTheDate()
    {
        var purchase = NewPurchase();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => purchase.UpdateDate(new DateOnly(9999, 12, 31), new DateOnly(2026, 10, 20)));

        Assert.Equal(DefaultDate, purchase.Date);
    }

    // ---------- UpdateDescription and UpdateCategory ----------

    [Fact]
    public void UpdateDescription_ChangesAndTrims()
    {
        var purchase = NewPurchase();

        purchase.UpdateDescription("  Geladeira  ");

        Assert.Equal("Geladeira", purchase.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDescription_Empty_ThrowsAndKeepsTheDescription(string? description)
    {
        var purchase = NewPurchase(description: "Notebook");

        Assert.ThrowsAny<ArgumentException>(() => purchase.UpdateDescription(description!));

        Assert.Equal("Notebook", purchase.Description);
    }

    [Fact]
    public void UpdateCategory_Changes()
    {
        var purchase = NewPurchase(category: PurchaseCategory.Shopping);

        purchase.UpdateCategory(PurchaseCategory.Education);

        Assert.Equal(PurchaseCategory.Education, purchase.Category);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void UpdateCategory_Unknown_ThrowsAndKeepsTheCategory(int category)
    {
        var purchase = NewPurchase(category: PurchaseCategory.Shopping);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => purchase.UpdateCategory((PurchaseCategory)category));

        Assert.Equal(PurchaseCategory.Shopping, purchase.Category);
    }

    [Fact]
    public void DescriptionAndCategory_RemainEditableAfterTheClosing()
    {
        var purchase = NewPurchase();
        Assert.True(purchase.IsLockedAt(new DateOnly(2026, 12, 1)));

        purchase.UpdateDescription("Notebook Dell");
        purchase.UpdateCategory(PurchaseCategory.Services);

        Assert.Equal("Notebook Dell", purchase.Description);
        Assert.Equal(PurchaseCategory.Services, purchase.Category);
    }

    // ---------- Main invariants ----------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(12)]
    [InlineData(24)]
    [InlineData(36)]
    public void Installments_SumToTheOriginalAmount_AndFollowTheCycleAndPaidRules(int count)
    {
        var initials = new[] { 1, (count + 1) / 2, count }.Distinct().ToArray();

        for (long cents = count; cents <= 2000; cents++)
        {
            foreach (var initial in initials)
            {
                var purchase = NewPurchase(amount: cents / 100m, count: count, initial: initial);
                var installments = purchase.GetInstallments();
                var context = $"amount={purchase.Amount.Amount}, count={count}, initial={initial}";

                Assert.True(installments.Count == count, $"Wrong number of installments: {context}");
                Assert.True(
                    Money.Sum(installments.Select(i => i.Amount)) == purchase.Amount,
                    $"Sum differs from the original amount: {context}");

                for (var i = 0; i < count; i++)
                {
                    var installment = installments[i];

                    Assert.True(installment.Number == i + 1, $"Wrong number at index {i}: {context}");
                    Assert.True(
                        installment.IsPreviouslyPaid == (installment.Number < initial),
                        $"Wrong paid flag at installment {installment.Number}: {context}");
                    Assert.True(
                        installment.Cycle == purchase.FirstCycle.AddCycles(i),
                        $"Wrong cycle at installment {installment.Number}: {context}");
                }
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    public void IsLockedAt_FlipsOnceAtTheClosingDate_AndNeverGoesBack(int initial)
    {
        var purchase = NewPurchase(count: 6, initial: initial);
        var closingDate = purchase.FirstCycle.AddCycles(initial - 1).ClosingDate;

        DateOnly? firstLocked = null;
        for (var day = purchase.Date.AddDays(-400); day <= closingDate.AddDays(400); day = day.AddDays(1))
        {
            var locked = purchase.IsLockedAt(day);

            if (locked && firstLocked is null)
            {
                firstLocked = day;
            }

            Assert.True(
                firstLocked is null || locked,
                $"Purchase unlocked again on {day:yyyy-MM-dd} (initial={initial})");
        }

        Assert.NotNull(firstLocked);
        Assert.Equal(closingDate, firstLocked.Value);
    }
}