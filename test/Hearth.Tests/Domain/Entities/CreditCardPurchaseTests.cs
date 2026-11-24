using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.Entities;

public class CreditCardPurchaseTests
{
    private static readonly CardBillingRule Rule3 = CardBillingRule.Of(3, 3);
    private static readonly DateOnly DefaultDate = new(2026, 10, 10);

    private static readonly Guid DefaultCardId = Guid.NewGuid();
    private static readonly Guid DefaultCategoryId = Guid.NewGuid();
    private static readonly Guid OthersConditionId = Guid.NewGuid();
    private static readonly Guid AnaConditionId = Guid.NewGuid();

    private static Money M(decimal amount) => Money.FromDecimal(amount);

    private static CreditCardPurchase NewPurchase(
    DateOnly? date = null,
    decimal amount = 100.00m,
    int count = 3,
    int initial = 1,
    CardBillingRule? rule = null,
    Guid? conditionId = null,
    string description = "Notebook",
    Guid? categoryId = null,
    Guid? accountId = null,
    Guid? creditCardId = null) =>
    CreditCardPurchase.Create(
        Guid.NewGuid(),
        accountId ?? Guid.NewGuid(),
        creditCardId ?? DefaultCardId,
        date ?? DefaultDate,
        M(amount),
        count,
        initial,
        rule ?? Rule3,
        conditionId ?? SystemPurchaseConditions.OwnId,
        description,
        categoryId ?? DefaultCategoryId);

    // ---------- Create ----------

    [Fact]
    public void Create_StoresTheGivenValues_AndTrimsTheDescription()
    {
        var id = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var purchase = CreditCardPurchase.Create(
            id,
            accountId,
            cardId,
            DefaultDate,
            M(250.00m),
            5,
            2,
            Rule3,
            OthersConditionId,
            "  Tênis  ",
            categoryId);

        Assert.Equal(id, purchase.Id);
        Assert.Equal(accountId, purchase.AccountId);
        Assert.Equal(cardId, purchase.CreditCardId);
        Assert.Equal(DefaultDate, purchase.Date);
        Assert.Equal(M(250.00m), purchase.Amount);
        Assert.Equal(5, purchase.InstallmentCount);
        Assert.Equal(2, purchase.InitialInstallment);
        Assert.Equal(Rule3, purchase.Rule);
        Assert.Equal(OthersConditionId, purchase.ConditionId);
        Assert.Equal("Tênis", purchase.Description);
        Assert.Equal(categoryId, purchase.CategoryId);
    }

    [Fact]
    public void Create_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() => CreditCardPurchase.Create(
            Guid.Empty, Guid.NewGuid(), DefaultCardId, DefaultDate, M(100m), 3, 1, Rule3,
            SystemPurchaseConditions.OwnId, "x", DefaultCategoryId));
    }

    [Fact]
    public void Create_EmptyCreditCardId_Throws()
    {
        Assert.Throws<ArgumentException>(() => CreditCardPurchase.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, DefaultDate, M(100m), 3, 1, Rule3,
            SystemPurchaseConditions.OwnId, "x", DefaultCategoryId));
    }

    [Fact]
    public void Create_NullRule_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CreditCardPurchase.Create(
            Guid.NewGuid(), Guid.NewGuid(), DefaultCardId, DefaultDate, M(100m), 3, 1, null!,
            SystemPurchaseConditions.OwnId, "x", DefaultCategoryId));
    }

    [Fact]
    public void Create_EmptyConditionId_Throws()
    {
        Assert.Throws<ArgumentException>(() => CreditCardPurchase.Create(
            Guid.NewGuid(), Guid.NewGuid(), DefaultCardId, DefaultDate, M(100m), 3, 1, Rule3,
            Guid.Empty, "x", DefaultCategoryId));
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
        Assert.Throws<DomainException>(() => NewPurchase(amount: 0.02m, count: 3));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_DescriptionIsRequired(string? description)
    {
        Assert.ThrowsAny<ArgumentException>(() => NewPurchase(description: description!));
    }

    [Fact]
    public void Create_DescriptionExceedingMaxLength_Throws()
    {
        var tooLong = new string('a', Hearth.Domain.DomainValidation.MaxDescriptionLength + 1);

        Assert.Throws<ArgumentException>(() => NewPurchase(description: tooLong));
    }

    [Fact]
    public void Create_DescriptionAtExactMaxLength_IsAccepted()
    {
        var exact = new string('a', Hearth.Domain.DomainValidation.MaxDescriptionLength);

        var purchase = NewPurchase(description: $"  {exact}  ");

        Assert.Equal(exact, purchase.Description);
    }

    [Fact]
    public void Create_EmptyCategoryId_Throws()
    {
        Assert.Throws<ArgumentException>(() => NewPurchase(categoryId: Guid.Empty));
    }

    [Fact]
    public void Create_DateWhoseCycleIsOutsideTheSupportedYears_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewPurchase(date: new DateOnly(9999, 12, 31)));
    }

    [Fact]
    public void Create_InstallmentsThatReachBeyondTheSupportedYears_Throw()
    {
        // With rule (3, 12), 9999-11-01 closes in 9999-11 (valid) and 2nd installment in 9999-12 (valid),
        // while the 3rd installment overflows into year 10000.
        var rule = CardBillingRule.Of(3, 12);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => NewPurchase(date: new DateOnly(9999, 11, 1), count: 3, rule: rule));
    }

    [Fact]
    public void FirstActiveCycle_AdvancesByInitialInstallmentMinusOne()
    {
        // Purchase on 2026-10-10 with closing day 3 -> installment 1 closes 2026-11, installment 3 closes 2027-01.
        var purchase = NewPurchase(date: new DateOnly(2026, 10, 10), count: 5, initial: 3, rule: CardBillingRule.Of(3, 12));

        Assert.Equal(YearMonth.Of(2026, 11), purchase.FirstCycle.ClosingMonth);
        Assert.Equal(YearMonth.Of(2027, 1), purchase.FirstActiveCycle.ClosingMonth);
    }

    [Fact]
    public void Create_EmptyAccountId_Throws()
    {
        Assert.Throws<ArgumentException>(() => NewPurchase(accountId: Guid.Empty));
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
            new[] { M(33.34m), M(33.33m), M(33.33m) },
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
        var own = NewPurchase(conditionId: SystemPurchaseConditions.OwnId).GetInstallments();
        var others = NewPurchase(conditionId: OthersConditionId).GetInstallments();
        var custom = NewPurchase(conditionId: AnaConditionId).GetInstallments();

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
    [InlineData(2026, 11, 2, false)]  // Open
    [InlineData(2026, 11, 3, false)]  // Closed, not yet paid
    [InlineData(2026, 11, 11, false)] // Day before due date (Closed, still unpaid)
    [InlineData(2026, 11, 12, true)]  // Paid on due date
    [InlineData(2026, 12, 1, true)]   // Paid
    public void IsLockedAt_PurchaseOnTheDefaultDate_LocksOnTheDueDateOfTheFirstInvoice(
        int year, int month, int day, bool expected)
    {
        // Rule (3, 12): first invoice closes on 03/11/2026 and is due (paid) on 12/11/2026.
        var purchase = NewPurchase(rule: CardBillingRule.Of(3, 12));

        Assert.Equal(expected, purchase.IsLockedAt(new DateOnly(year, month, day)));
    }

    [Theory]
    [InlineData(2026, 10, 2, false)]  // Open
    [InlineData(2026, 10, 3, false)]  // Closed (due 12/10)
    [InlineData(2026, 10, 11, false)] // Closed, day before due
    [InlineData(2026, 10, 12, true)]  // Paid on due date
    public void IsLockedAt_PurchaseBeforeTheClosingDay_LocksOnTheSameMonthDueDate(
        int year, int month, int day, bool expected)
    {
        var purchase = NewPurchase(date: new DateOnly(2026, 10, 2), rule: CardBillingRule.Of(3, 12));

        Assert.Equal(expected, purchase.IsLockedAt(new DateOnly(year, month, day)));
    }

    [Theory]
    [InlineData(2026, 10, 20, false)]
    [InlineData(2026, 11, 3, false)]  // Closed, not yet paid
    [InlineData(2026, 11, 11, false)] // Day before due date
    [InlineData(2026, 11, 12, true)]  // Installment 4 due date -> paid
    public void IsLockedAt_PurchaseInProgress_UsesTheFirstInstallmentThatStillCounts(
        int year, int month, int day, bool expected)
    {
        // Installments 1 to 3 are previously paid; installment 4 closes on 03/11/2026 and is due on 12/11/2026.
        var purchase = NewPurchase(
            date: new DateOnly(2026, 7, 10),
            amount: 600.00m,
            count: 6,
            initial: 4,
            rule: CardBillingRule.Of(3, 12));

        Assert.Equal(expected, purchase.IsLockedAt(new DateOnly(year, month, day)));
    }

    [Fact]
    public void IsLockedAt_PartiallyPaidMultiInstallmentPurchase_IsLockedOnceFirstInstallmentIsPaid()
    {
        // 3x purchase: installment 1 due 12/11/2026, installment 2 due 12/12/2026, installment 3 due 12/01/2027.
        var purchase = NewPurchase(
            date: new DateOnly(2026, 10, 10),
            amount: 300.00m,
            count: 3,
            initial: 1,
            rule: CardBillingRule.Of(3, 12));

        // On 2026-11-20: installment 1 is paid while installments 2 and 3 are still open -> must be locked.
        Assert.True(purchase.IsLockedAt(new DateOnly(2026, 11, 20)));
    }

    // ---------- UpdateAmount ----------

    [Fact]
    public void UpdateAmount_BeforeDueDateIncludingWhileClosed_ChangesTheAmountAndTheInstallments()
    {
        var purchase = NewPurchase(rule: CardBillingRule.Of(3, 12));

        // On 2026-11-05 the invoice is Closed (closed 03/11) but NOT yet Paid (due 12/11).
        purchase.UpdateAmount(M(250.00m), new DateOnly(2026, 11, 5));

        Assert.Equal(M(250.00m), purchase.Amount);
        Assert.Equal(
            new[] { M(83.34m), M(83.33m), M(83.33m) },
            purchase.GetInstallments().Select(i => i.Amount));
    }

    [Fact]
    public void UpdateAmount_OnOrAfterDueDate_ThrowsAndKeepsTheAmount()
    {
        var purchase = NewPurchase(rule: CardBillingRule.Of(3, 12));

        Assert.Throws<DomainException>(
            () => purchase.UpdateAmount(M(250.00m), new DateOnly(2026, 11, 12)));

        Assert.Equal(M(100.00m), purchase.Amount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void UpdateAmount_ZeroOrNegative_ThrowsAndKeepsTheAmount(double raw)
    {
        var purchase = NewPurchase(rule: CardBillingRule.Of(3, 12));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => purchase.UpdateAmount(M((decimal)raw), new DateOnly(2026, 10, 20)));

        Assert.Equal(M(100.00m), purchase.Amount);
    }

    [Fact]
    public void UpdateAmount_TooSmallToSplit_ThrowsAndKeepsTheAmount()
    {
        var purchase = NewPurchase(rule: CardBillingRule.Of(3, 12));

        Assert.Throws<DomainException>(
            () => purchase.UpdateAmount(M(0.02m), new DateOnly(2026, 10, 20)));

        Assert.Equal(M(100.00m), purchase.Amount);
    }

    // ---------- UpdateDate ----------

    [Fact]
    public void UpdateDate_BeforeDueDateIncludingWhileClosed_ChangesTheDateAndTheFirstCycle()
    {
        var purchase = NewPurchase(rule: CardBillingRule.Of(3, 12));

        // On 2026-11-05 invoice is Closed (closed 03/11) but not yet Paid (due 12/11).
        purchase.UpdateDate(new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 5));

        Assert.Equal(new DateOnly(2026, 11, 5), purchase.Date);
        Assert.Equal(YearMonth.Of(2026, 12), purchase.FirstCycle.ClosingMonth);
    }

    [Fact]
    public void UpdateDate_OnOrAfterDueDate_ThrowsAndKeepsTheDate()
    {
        var purchase = NewPurchase(rule: CardBillingRule.Of(3, 12));

        Assert.Throws<DomainException>(
            () => purchase.UpdateDate(new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 12)));

        Assert.Equal(DefaultDate, purchase.Date);
    }

    [Fact]
    public void UpdateDate_MovingIntoAlreadyPaidInvoice_ThrowsAndKeepsTheDate()
    {
        // Today is 20/10/2026, rule (3, 12) -> October invoice was paid on 12/10/2026.
        // Moving from 10/10/2026 (Nov invoice, due 12/11) to 02/10/2026 (Oct invoice, paid 12/10) must fail.
        var purchase = NewPurchase(date: new DateOnly(2026, 10, 10), rule: CardBillingRule.Of(3, 12));

        Assert.Throws<DomainException>(
            () => purchase.UpdateDate(new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 20)));

        Assert.Equal(new DateOnly(2026, 10, 10), purchase.Date);
    }

    [Fact]
    public void UpdateDate_PurchaseInProgress_MovingFirstCountingCycleIntoPaidInvoice_ThrowsAndKeepsTheDate()
    {
        // Purchase on 10/07/2026, 6x, initial 4, rule (3, 12) -> installment 4 due 12/11/2026 (unpaid on 20/10/2026).
        // Moving to 10/06/2026 shifts installment 4 to due 12/10/2026 (already paid on 20/10/2026).
        var purchase = NewPurchase(
            date: new DateOnly(2026, 7, 10),
            amount: 600.00m,
            count: 6,
            initial: 4,
            rule: CardBillingRule.Of(3, 12));

        Assert.Throws<DomainException>(
            () => purchase.UpdateDate(new DateOnly(2026, 6, 10), new DateOnly(2026, 10, 20)));

        Assert.Equal(new DateOnly(2026, 7, 10), purchase.Date);
    }

    [Fact]
    public void UpdateDate_MovingEarlierIntoClosedButUnpaidInvoice_IsAccepted()
    {
        // Today is 10/10/2026, rule (3, 12) -> October invoice closed 03/10 but is NOT paid until 12/10.
        var purchase = NewPurchase(date: new DateOnly(2026, 10, 10), rule: CardBillingRule.Of(3, 12));

        purchase.UpdateDate(new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 10));

        Assert.Equal(new DateOnly(2026, 10, 2), purchase.Date);
        Assert.Equal(YearMonth.Of(2026, 10), purchase.FirstCycle.ClosingMonth);
    }

    [Fact]
    public void UpdateDate_MovingIntoCycleDueExactlyOnToday_ThrowsAndKeepsTheDate()
    {
        // Today is 12/10/2026 (exact due date of October invoice): moving to 02/10/2026 must fail.
        var purchase = NewPurchase(date: new DateOnly(2026, 10, 10), rule: CardBillingRule.Of(3, 12));

        Assert.Throws<DomainException>(
            () => purchase.UpdateDate(new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 12)));

        Assert.Equal(new DateOnly(2026, 10, 10), purchase.Date);
    }

    [Fact]
    public void UpdateDate_CycleOutsideTheSupportedYears_ThrowsAndKeepsTheDate()
    {
        var purchase = NewPurchase(rule: CardBillingRule.Of(3, 12), count: 3);

        // First installment valid (9999-11), third installment overflows year 9999.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => purchase.UpdateDate(new DateOnly(9999, 11, 1), new DateOnly(2026, 10, 20)));

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
    public void UpdateDescription_ExceedingMaxLength_ThrowsAndKeepsTheDescription()
    {
        var purchase = NewPurchase(description: "Notebook");
        var tooLong = new string('a', Hearth.Domain.DomainValidation.MaxDescriptionLength + 1);

        Assert.Throws<ArgumentException>(() => purchase.UpdateDescription(tooLong));

        Assert.Equal("Notebook", purchase.Description);
    }

    [Fact]
    public void UpdateCategory_Changes()
    {
        var originalCategory = Guid.NewGuid();
        var newCategory = Guid.NewGuid();
        var purchase = NewPurchase(categoryId: originalCategory);

        purchase.UpdateCategory(newCategory);

        Assert.Equal(newCategory, purchase.CategoryId);
    }

    [Fact]
    public void UpdateCategory_Empty_ThrowsAndKeepsTheCategory()
    {
        var originalCategory = Guid.NewGuid();
        var purchase = NewPurchase(categoryId: originalCategory);

        Assert.Throws<ArgumentException>(() => purchase.UpdateCategory(Guid.Empty));

        Assert.Equal(originalCategory, purchase.CategoryId);
    }

    [Fact]
    public void UpdateCondition_Changes()
    {
        var purchase = NewPurchase(conditionId: SystemPurchaseConditions.OwnId);

        purchase.UpdateCondition(OthersConditionId);
        Assert.Equal(OthersConditionId, purchase.ConditionId);

        purchase.UpdateCondition(AnaConditionId);
        Assert.Equal(AnaConditionId, purchase.ConditionId);
    }

    [Fact]
    public void UpdateCondition_Empty_ThrowsAndKeepsTheCondition()
    {
        var purchase = NewPurchase(conditionId: OthersConditionId);

        Assert.Throws<ArgumentException>(() => purchase.UpdateCondition(Guid.Empty));

        Assert.Equal(OthersConditionId, purchase.ConditionId);
    }

    [Fact]
    public void DescriptionCategoryAndCondition_RemainEditableAfterInvoiceIsPaid()
    {
        var newCategory = Guid.NewGuid();
        var momConditionId = Guid.NewGuid();
        var purchase = NewPurchase(rule: CardBillingRule.Of(3, 12));
        Assert.True(purchase.IsLockedAt(new DateOnly(2026, 12, 1)));

        purchase.UpdateDescription("Notebook Dell");
        purchase.UpdateCategory(newCategory);
        purchase.UpdateCondition(momConditionId);

        Assert.Equal("Notebook Dell", purchase.Description);
        Assert.Equal(newCategory, purchase.CategoryId);
        Assert.Equal(momConditionId, purchase.ConditionId);
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
    public void IsLockedAt_FlipsOnceAtTheDueDate_AndNeverGoesBack(int initial)
    {
        var purchase = NewPurchase(count: 6, initial: initial, rule: CardBillingRule.Of(3, 12));
        var dueDate = purchase.FirstCycle.AddCycles(initial - 1).DueDate;

        DateOnly? firstLocked = null;
        for (var day = purchase.Date.AddDays(-400); day <= dueDate.AddDays(400); day = day.AddDays(1))
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
        Assert.Equal(dueDate, firstLocked.Value);
    }
}