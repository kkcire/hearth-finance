using Hearth.Domain.Entities;
using Hearth.Domain.Services;
using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.Services;

public class InvoiceCalculatorTests
{
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly CardBillingRule Rule3 = CardBillingRule.Of(3, 12);

    private static Money M(decimal amount) => Money.FromDecimal(amount);

    private static YearMonth YM(int year, int month) => YearMonth.Of(year, month);

    private static DateOnly Day(int year, int month, int day) => new(year, month, day);

    private static CreditCardPurchase NewPurchase(
        DateOnly date,
        decimal amount = 100.00m,
        int count = 1,
        int initial = 1,
        Guid? accountId = null,
        CardBillingRule? rule = null,
        PurchaseCondition? condition = null) =>
        CreditCardPurchase.Create(
            Guid.NewGuid(),
            accountId ?? AccountId,
            date,
            M(amount),
            count,
            initial,
            rule ?? Rule3,
            condition ?? PurchaseCondition.Own,
            "Compra",
            ExpenseCategory.Shopping);

    private static Money Calc(YearMonth closingMonth, params CreditCardPurchase[] purchases) =>
        InvoiceCalculator.Calculate(AccountId, closingMonth, purchases);

    // ---------- Empty and trivial cases ----------

    [Fact]
    public void Calculate_NoPurchases_IsZero()
    {
        Assert.Equal(Money.Zero, Calc(YM(2026, 11)));
    }

    [Fact]
    public void Calculate_SingleInstallmentPurchase_CountsOnlyInItsOwnCycle()
    {
        // Purchase on 10/10 with closing day 3 goes to the invoice closing in November.
        var purchase = NewPurchase(Day(2026, 10, 10), amount: 100.00m);

        Assert.Equal(Money.Zero, Calc(YM(2026, 10), purchase));
        Assert.Equal(M(100.00m), Calc(YM(2026, 11), purchase));
        Assert.Equal(Money.Zero, Calc(YM(2026, 12), purchase));
    }

    [Fact]
    public void Calculate_PurchaseBeforeTheClosingDay_CountsInTheSameMonthInvoice()
    {
        var purchase = NewPurchase(Day(2026, 10, 2), amount: 100.00m);

        Assert.Equal(M(100.00m), Calc(YM(2026, 10), purchase));
        Assert.Equal(Money.Zero, Calc(YM(2026, 11), purchase));
    }

    [Fact]
    public void Calculate_PurchaseOnTheClosingDay_CountsInTheNextInvoice()
    {
        var purchase = NewPurchase(Day(2026, 10, 3), amount: 100.00m);

        Assert.Equal(Money.Zero, Calc(YM(2026, 10), purchase));
        Assert.Equal(M(100.00m), Calc(YM(2026, 11), purchase));
    }

    // ---------- Installments ----------

    [Fact]
    public void Calculate_Installments_FallOnConsecutiveInvoices_WithTheRemainderOnTheLast()
    {
        var purchase = NewPurchase(Day(2026, 10, 10), amount: 100.00m, count: 3);

        Assert.Equal(Money.Zero, Calc(YM(2026, 10), purchase));
        Assert.Equal(M(33.33m), Calc(YM(2026, 11), purchase));
        Assert.Equal(M(33.33m), Calc(YM(2026, 12), purchase));
        Assert.Equal(M(33.34m), Calc(YM(2027, 1), purchase));
        Assert.Equal(Money.Zero, Calc(YM(2027, 2), purchase));
    }

    [Fact]
    public void Calculate_Installments_AddUpToTheOriginalAmountAcrossInvoices()
    {
        var purchase = NewPurchase(Day(2026, 12, 15), amount: 100.00m, count: 3);

        var total = Money.Sum(new[]
        {
            Calc(YM(2027, 1), purchase),
            Calc(YM(2027, 2), purchase),
            Calc(YM(2027, 3), purchase),
        });

        Assert.Equal(M(100.00m), total);
    }

    [Fact]
    public void Calculate_RemainderLargerThanOneCent_GoesToTheLastInvoice()
    {
        var purchase = NewPurchase(Day(2026, 10, 10), amount: 0.05m, count: 3);

        Assert.Equal(M(0.01m), Calc(YM(2026, 11), purchase));
        Assert.Equal(M(0.01m), Calc(YM(2026, 12), purchase));
        Assert.Equal(M(0.03m), Calc(YM(2027, 1), purchase));
    }

    [Fact]
    public void Calculate_SeveralPurchasesInTheSameCycle_AreSummed()
    {
        var first = NewPurchase(Day(2026, 10, 10), amount: 100.00m);
        var second = NewPurchase(Day(2026, 10, 20), amount: 50.50m);
        var third = NewPurchase(Day(2026, 10, 25), amount: 300.00m, count: 3);

        // November gets 100.00 + 50.50 + the first installment (100.00) of the third purchase.
        Assert.Equal(M(250.50m), Calc(YM(2026, 11), first, second, third));
        Assert.Equal(M(100.00m), Calc(YM(2026, 12), first, second, third));
    }

    // ---------- Purchases in progress ----------

    [Fact]
    public void Calculate_PurchaseInProgress_IgnoresTheInstallmentsConsideredPaid()
    {
        // Original purchase on 10/07/2026 in 6 installments of 100.00, now at installment 4.
        // Installments 1 to 3 would close in August, September and October.
        var purchase = NewPurchase(Day(2026, 7, 10), amount: 600.00m, count: 6, initial: 4);

        Assert.Equal(Money.Zero, Calc(YM(2026, 8), purchase));
        Assert.Equal(Money.Zero, Calc(YM(2026, 9), purchase));
        Assert.Equal(Money.Zero, Calc(YM(2026, 10), purchase));
        Assert.Equal(M(100.00m), Calc(YM(2026, 11), purchase));
        Assert.Equal(M(100.00m), Calc(YM(2026, 12), purchase));
        Assert.Equal(M(100.00m), Calc(YM(2027, 1), purchase));
        Assert.Equal(Money.Zero, Calc(YM(2027, 2), purchase));
    }

    [Fact]
    public void Calculate_PurchaseInProgress_OnlyTheLastInstallmentCounts()
    {
        var purchase = NewPurchase(Day(2026, 1, 10), amount: 1200.00m, count: 12, initial: 12);

        for (var month = YM(2026, 2); month < YM(2027, 1); month = month.AddMonths(1))
        {
            Assert.Equal(Money.Zero, Calc(month, purchase));
        }

        Assert.Equal(M(100.00m), Calc(YM(2027, 1), purchase));
    }

    [Fact]
    public void Calculate_PurchaseInProgress_KeepsTheRemainderOnTheLastCountingInstallment()
    {
        // 100.00 in 3 from installment 2: the installments are 33.33, 33.33 and 33.34.
        var purchase = NewPurchase(Day(2026, 10, 10), amount: 100.00m, count: 3, initial: 2);

        Assert.Equal(Money.Zero, Calc(YM(2026, 11), purchase));
        Assert.Equal(M(33.33m), Calc(YM(2026, 12), purchase));
        Assert.Equal(M(33.34m), Calc(YM(2027, 1), purchase));
    }

    // ---------- Accounts ----------

    [Fact]
    public void Calculate_IgnoresPurchasesOfOtherAccounts()
    {
        var mine = NewPurchase(Day(2026, 10, 10), amount: 100.00m);
        var theirs = NewPurchase(Day(2026, 10, 10), amount: 999.00m, accountId: Guid.NewGuid());

        Assert.Equal(M(100.00m), Calc(YM(2026, 11), mine, theirs));
    }

    [Fact]
    public void Calculate_OnlyPurchasesOfOtherAccounts_IsZero()
    {
        var theirs = NewPurchase(Day(2026, 10, 10), amount: 999.00m, accountId: Guid.NewGuid());

        Assert.Equal(Money.Zero, Calc(YM(2026, 11), theirs));
    }

    // ---------- Condition ----------

    [Fact]
    public void Calculate_PurchaseCondition_NeverChangesTheResult()
    {
        var own = NewPurchase(Day(2026, 10, 10), amount: 100.00m, condition: PurchaseCondition.Own);
        var others = NewPurchase(Day(2026, 10, 10), amount: 100.00m, condition: PurchaseCondition.Others);
        var custom = NewPurchase(
            Day(2026, 10, 10), amount: 100.00m, condition: PurchaseCondition.Custom("Ana"));

        Assert.Equal(M(100.00m), Calc(YM(2026, 11), own));
        Assert.Equal(M(100.00m), Calc(YM(2026, 11), others));
        Assert.Equal(M(100.00m), Calc(YM(2026, 11), custom));
        Assert.Equal(M(300.00m), Calc(YM(2026, 11), own, others, custom));
    }

    // ---------- Cycle identity ----------

    [Fact]
    public void Calculate_MatchesTheInvoiceByClosingMonth_EvenWithDifferentClosingDays()
    {
        // Purchase A (closing day 3) and B (closing day 15) both go to the November invoice;
        // C (closing day 15) bought on 10/10 still goes to October.
        var a = NewPurchase(Day(2026, 10, 10), amount: 100.00m, rule: CardBillingRule.Of(3, 12));
        var b = NewPurchase(Day(2026, 10, 20), amount: 50.00m, rule: CardBillingRule.Of(15, 12));
        var c = NewPurchase(Day(2026, 10, 10), amount: 30.00m, rule: CardBillingRule.Of(15, 12));

        Assert.Equal(M(150.00m), Calc(YM(2026, 11), a, b, c));
        Assert.Equal(M(30.00m), Calc(YM(2026, 10), a, b, c));
    }

    [Fact]
    public void Calculate_DueDayDoesNotAffectWhichInvoiceAPurchaseBelongsTo()
    {
        var a = NewPurchase(Day(2026, 10, 10), amount: 100.00m, rule: CardBillingRule.Of(3, 12));
        var b = NewPurchase(Day(2026, 10, 10), amount: 50.00m, rule: CardBillingRule.Of(3, 20));

        Assert.Equal(M(150.00m), Calc(YM(2026, 11), a, b));
    }

    // ---------- Arguments and stability ----------

    [Fact]
    public void Calculate_EmptyAccountId_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => InvoiceCalculator.Calculate(Guid.Empty, YM(2026, 11), Array.Empty<CreditCardPurchase>()));
    }

    [Fact]
    public void Calculate_NullClosingMonth_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => InvoiceCalculator.Calculate(AccountId, null!, Array.Empty<CreditCardPurchase>()));
    }

    [Fact]
    public void Calculate_NullPurchases_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => InvoiceCalculator.Calculate(AccountId, YM(2026, 11), null!));
    }

    [Fact]
    public void Calculate_CalledTwice_ReturnsTheSameValue_AndDoesNotChangeThePurchases()
    {
        var purchase = NewPurchase(Day(2026, 10, 10), amount: 100.00m, count: 3);
        var installmentsBefore = purchase.GetInstallments();

        var first = Calc(YM(2026, 12), purchase);
        var second = Calc(YM(2026, 12), purchase);

        Assert.Equal(first, second);
        Assert.Equal(installmentsBefore, purchase.GetInstallments());
        Assert.Equal(M(100.00m), purchase.Amount);
    }

    // ---------- Main invariants ----------

    [Fact]
    public void SumOverAllMonths_EqualsTheCountingAmountOfTheAccountsPurchases()
    {
        var random = new Random(20261028);
        var otherAccount = Guid.NewGuid();
        var purchases = new List<CreditCardPurchase>();
        var expectedTotal = Money.Zero;

        for (var i = 0; i < 300; i++)
        {
            var count = random.Next(1, 25);
            var initial = random.Next(1, count + 1);
            var cents = random.Next(count, 200000);
            var date = Day(2026, 1, 1).AddDays(random.Next(0, 700));
            var rule = CardBillingRule.Of(random.Next(1, 29), random.Next(1, 29));
            var belongsToAccount = random.Next(4) != 0;

            var purchase = NewPurchase(
                date,
                amount: cents / 100m,
                count: count,
                initial: initial,
                accountId: belongsToAccount ? AccountId : otherAccount,
                rule: rule);

            purchases.Add(purchase);

            if (belongsToAccount)
            {
                // Independent of the cycles: only the amounts of the installments that still count.
                expectedTotal += Money.Sum(purchase.Amount.Split(count).Skip(initial - 1));
            }
        }

        var total = Money.Zero;
        for (var month = YM(2025, 1); month <= YM(2033, 12); month = month.AddMonths(1))
        {
            total += InvoiceCalculator.Calculate(AccountId, month, purchases);
        }

        Assert.Equal(expectedTotal, total);
    }

    [Fact]
    public void EachMonth_MatchesAnIndependentCalculationOfTheCycles_InAnyOrder()
    {
        var random = new Random(20261029);
        var purchases = new List<CreditCardPurchase>();

        for (var i = 0; i < 150; i++)
        {
            var count = random.Next(1, 25);
            var initial = random.Next(1, count + 1);
            var cents = random.Next(count, 200000);
            var date = Day(2026, 1, 1).AddDays(random.Next(0, 700));
            var rule = CardBillingRule.Of(random.Next(1, 29), random.Next(1, 29));

            purchases.Add(NewPurchase(date, cents / 100m, count, initial, rule: rule));
        }

        var reversed = purchases.AsEnumerable().Reverse().ToList();

        for (var month = YM(2025, 12); month <= YM(2030, 12); month = month.AddMonths(1))
        {
            var expected = Money.Zero;

            foreach (var purchase in purchases)
            {
                var amounts = purchase.Amount.Split(purchase.InstallmentCount);
                var firstClosing = BillingCycle.ForPurchase(purchase.Date, purchase.Rule).ClosingMonth;

                for (var k = purchase.InitialInstallment - 1; k < purchase.InstallmentCount; k++)
                {
                    if (firstClosing.AddMonths(k) == month)
                    {
                        expected += amounts[k];
                    }
                }
            }

            var context = $"month={month.Year}-{month.Month:00}";

            Assert.True(
                InvoiceCalculator.Calculate(AccountId, month, purchases) == expected,
                $"Calculated invoice differs from the independent calculation: {context}");
            Assert.True(
                InvoiceCalculator.Calculate(AccountId, month, reversed) == expected,
                $"The result depends on the order of the purchases: {context}");
        }
    }
}