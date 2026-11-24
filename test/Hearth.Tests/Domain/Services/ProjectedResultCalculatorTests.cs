using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.Services;
using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.Services;

public class ProjectedResultCalculatorTests
{
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid OtherAccountId = Guid.NewGuid();
    private static readonly Guid CardId = Guid.NewGuid();
    private static readonly CardBillingRule Rule3 = CardBillingRule.Of(3, 12);

    private static Money M(decimal amount) => Money.FromDecimal(amount);

    private static YearMonth YM(int year, int month) => YearMonth.Of(year, month);

    private static DateOnly Day(int year, int month, int day) => new(year, month, day);

    private static Income NewIncome(
        DateOnly expectedDate, decimal amount, string description = "Salário", Guid? accountId = null) =>
        Income.Create(Guid.NewGuid(), accountId ?? AccountId, expectedDate, M(amount), description);

    private static DirectExpense NewDirect(DateOnly date, decimal amount, Guid? accountId = null) =>
        DirectExpense.Create(
            Guid.NewGuid(), accountId ?? AccountId, date, M(amount), "Mercado", Guid.NewGuid());

    private static FixedExpense NewFixed(
        YearMonth start, YearMonth? end, decimal amount, Guid? accountId = null) =>
        FixedExpense.Create(
            Guid.NewGuid(), accountId ?? AccountId, start, end, M(amount), "Aluguel", Guid.NewGuid());

    private static CreditCardPurchase NewPurchase(
        DateOnly date,
        decimal amount,
        int count = 1,
        int initial = 1,
        Guid? accountId = null,
        CardBillingRule? rule = null) =>
        CreditCardPurchase.Create(
            Guid.NewGuid(),
            accountId ?? AccountId,
            CardId,
            date,
            M(amount),
            count,
            initial,
            rule ?? Rule3,
            SystemPurchaseConditions.OwnId,
            "Compra",
            Guid.NewGuid());

    private static ProjectedResult Calc(
        YearMonth month,
        IEnumerable<Income>? incomes = null,
        IEnumerable<DirectExpense>? direct = null,
        IEnumerable<FixedExpense>? fixedExpenses = null,
        IEnumerable<CreditCardPurchase>? purchases = null) =>
        ProjectedResultCalculator.Calculate(
            AccountId,
            month,
            incomes ?? Array.Empty<Income>(),
            direct ?? Array.Empty<DirectExpense>(),
            fixedExpenses ?? Array.Empty<FixedExpense>(),
            purchases ?? Array.Empty<CreditCardPurchase>());

    // ---------- Empty ----------

    [Fact]
    public void Calculate_WithNoData_IsAllZero_AndKeepsTheMonth()
    {
        var result = Calc(YM(2026, 11));

        Assert.Equal(YM(2026, 11), result.Month);
        Assert.Equal(Money.Zero, result.Incomes);
        Assert.Equal(Money.Zero, result.CardInvoice);
        Assert.Equal(Money.Zero, result.DirectExpenses);
        Assert.Equal(Money.Zero, result.FixedExpenses);
        Assert.Equal(Money.Zero, result.Result);
    }

    // ---------- Incomes ----------

    [Fact]
    public void Incomes_CountByTheExpectedMonth()
    {
        var income = NewIncome(Day(2026, 10, 28), 5000.00m);

        Assert.Equal(Money.Zero, Calc(YM(2026, 9), incomes: new[] { income }).Incomes);
        Assert.Equal(M(5000.00m), Calc(YM(2026, 10), incomes: new[] { income }).Incomes);
        Assert.Equal(Money.Zero, Calc(YM(2026, 11), incomes: new[] { income }).Incomes);
    }

    [Fact]
    public void Incomes_FirstAndLastDayOfTheMonth_BelongToThatMonth()
    {
        var first = NewIncome(Day(2026, 10, 1), 100.00m);
        var last = NewIncome(Day(2026, 10, 31), 200.00m);
        var before = NewIncome(Day(2026, 9, 30), 400.00m);
        var after = NewIncome(Day(2026, 11, 1), 800.00m);

        var result = Calc(YM(2026, 10), incomes: new[] { first, last, before, after });

        Assert.Equal(M(300.00m), result.Incomes);
    }

    [Fact]
    public void Incomes_SalaryAndOtherIncomes_AreSummed()
    {
        var salary = NewIncome(Day(2026, 11, 5), 5000.00m, "Salário");
        var pix = NewIncome(Day(2026, 11, 20), 200.00m, "Pix da mãe");

        Assert.Equal(M(5200.00m), Calc(YM(2026, 11), incomes: new[] { salary, pix }).Incomes);
    }

    [Fact]
    public void Incomes_CountWhetherOrNotTheyWereReceived()
    {
        var expected = NewIncome(Day(2026, 11, 5), 100.00m);
        var received = NewIncome(Day(2026, 11, 5), 100.00m);
        received.MarkAsReceived(Day(2026, 11, 5), Day(2026, 12, 31));

        Assert.Equal(M(100.00m), Calc(YM(2026, 11), incomes: new[] { expected }).Incomes);
        Assert.Equal(M(100.00m), Calc(YM(2026, 11), incomes: new[] { received }).Incomes);
        Assert.Equal(M(200.00m), Calc(YM(2026, 11), incomes: new[] { expected, received }).Incomes);
    }

    [Fact]
    public void Incomes_ReceivedInAnotherMonth_StillCountInTheExpectedMonth()
    {
        // Expected on 28/10, received on 02/11: October by accrual.
        var income = NewIncome(Day(2026, 10, 28), 5000.00m);
        income.MarkAsReceived(Day(2026, 11, 2), Day(2026, 12, 31));

        Assert.Equal(M(5000.00m), Calc(YM(2026, 10), incomes: new[] { income }).Incomes);
        Assert.Equal(Money.Zero, Calc(YM(2026, 11), incomes: new[] { income }).Incomes);
    }

    // ---------- Direct expenses ----------

    [Fact]
    public void DirectExpenses_CountByTheirDate_IncludingTheFirstAndLastDayOfTheMonth()
    {
        var first = NewDirect(Day(2026, 10, 1), 10.00m);
        var middle = NewDirect(Day(2026, 10, 15), 20.00m);
        var last = NewDirect(Day(2026, 10, 31), 40.00m);
        var before = NewDirect(Day(2026, 9, 30), 80.00m);
        var after = NewDirect(Day(2026, 11, 1), 160.00m);

        var result = Calc(YM(2026, 10), direct: new[] { first, middle, last, before, after });

        Assert.Equal(M(70.00m), result.DirectExpenses);
    }

    [Fact]
    public void DirectExpenses_DoNotDependOnTheCardClosingDay()
    {
        // The closing day is 3, but a direct expense never enters the invoice.
        var expense = NewDirect(Day(2026, 10, 5), 50.00m);

        var result = Calc(YM(2026, 10), direct: new[] { expense });

        Assert.Equal(M(50.00m), result.DirectExpenses);
        Assert.Equal(Money.Zero, result.CardInvoice);
    }

    // ---------- Fixed expenses ----------

    [Fact]
    public void FixedExpenses_CountOnlyInTheirActiveMonths()
    {
        var expense = NewFixed(YM(2026, 10), YM(2026, 12), 1000.00m);
        var expenses = new[] { expense };

        Assert.Equal(Money.Zero, Calc(YM(2026, 9), fixedExpenses: expenses).FixedExpenses);
        Assert.Equal(M(1000.00m), Calc(YM(2026, 10), fixedExpenses: expenses).FixedExpenses);
        Assert.Equal(M(1000.00m), Calc(YM(2026, 11), fixedExpenses: expenses).FixedExpenses);
        Assert.Equal(M(1000.00m), Calc(YM(2026, 12), fixedExpenses: expenses).FixedExpenses);
        Assert.Equal(Money.Zero, Calc(YM(2027, 1), fixedExpenses: expenses).FixedExpenses);
    }

    [Fact]
    public void FixedExpenses_WithoutAnEnd_KeepCountingForever()
    {
        var expense = NewFixed(YM(2026, 10), null, 1000.00m);

        Assert.Equal(Money.Zero, Calc(YM(2026, 9), fixedExpenses: new[] { expense }).FixedExpenses);
        Assert.Equal(M(1000.00m), Calc(YM(2040, 6), fixedExpenses: new[] { expense }).FixedExpenses);
    }

    [Fact]
    public void FixedExpenses_CountWhetherOrNotTheyWerePaid()
    {
        var unpaid = NewFixed(YM(2026, 1), null, 1000.00m);
        var paid = NewFixed(YM(2026, 1), null, 1000.00m);
        paid.MarkAsPaid(YM(2026, 10), Day(2026, 10, 5), Day(2026, 12, 31));

        Assert.Equal(M(1000.00m), Calc(YM(2026, 10), fixedExpenses: new[] { unpaid }).FixedExpenses);
        Assert.Equal(M(1000.00m), Calc(YM(2026, 10), fixedExpenses: new[] { paid }).FixedExpenses);
    }

    [Fact]
    public void FixedExpenses_PaidInAnotherMonth_StillCountInTheirCompetenceMonth()
    {
        // October's expense paid on 02/11: October by accrual.
        var expense = NewFixed(YM(2026, 1), YM(2026, 10), 1000.00m);
        expense.MarkAsPaid(YM(2026, 10), Day(2026, 11, 2), Day(2026, 12, 31));

        Assert.Equal(M(1000.00m), Calc(YM(2026, 10), fixedExpenses: new[] { expense }).FixedExpenses);
        Assert.Equal(Money.Zero, Calc(YM(2026, 11), fixedExpenses: new[] { expense }).FixedExpenses);
    }

    [Fact]
    public void FixedExpenses_UseTheAmountOfEachMonth()
    {
        var expense = NewFixed(YM(2026, 1), null, 1000.00m);
        // Changed in July, so June remains frozen at 1000 and the new amount applies from July.
        expense.ChangeAmount(M(1200.00m), Day(2026, 7, 10));

        Assert.Equal(M(1000.00m), Calc(YM(2026, 6), fixedExpenses: new[] { expense }).FixedExpenses);
        Assert.Equal(M(1200.00m), Calc(YM(2026, 7), fixedExpenses: new[] { expense }).FixedExpenses);
        Assert.Equal(M(1200.00m), Calc(YM(2027, 3), fixedExpenses: new[] { expense }).FixedExpenses);
    }

    [Fact]
    public void FixedExpenses_SeveralActiveOnes_AreSummed()
    {
        var rent = NewFixed(YM(2026, 1), null, 1000.00m);
        var internet = NewFixed(YM(2026, 5), null, 99.90m);
        var ended = NewFixed(YM(2026, 1), YM(2026, 8), 500.00m);

        var result = Calc(YM(2026, 11), fixedExpenses: new[] { rent, internet, ended });

        Assert.Equal(M(1099.90m), result.FixedExpenses);
    }

    // ---------- Card invoice ----------

    [Fact]
    public void CardInvoice_IsTheCalculatedInvoiceOfTheCycleClosingInTheMonth()
    {
        // Purchase on 10/10 with closing day 3: first installment on the invoice closing in November.
        var purchase = NewPurchase(Day(2026, 10, 10), 100.00m, count: 3);
        var purchases = new[] { purchase };

        Assert.Equal(Money.Zero, Calc(YM(2026, 10), purchases: purchases).CardInvoice);
        Assert.Equal(M(33.34m), Calc(YM(2026, 11), purchases: purchases).CardInvoice);
        Assert.Equal(M(33.33m), Calc(YM(2026, 12), purchases: purchases).CardInvoice);
        Assert.Equal(M(33.33m), Calc(YM(2027, 1), purchases: purchases).CardInvoice);
        Assert.Equal(Money.Zero, Calc(YM(2027, 2), purchases: purchases).CardInvoice);
    }

    [Fact]
    public void CardInvoice_PurchaseBeforeTheClosingDay_CountsInTheSameMonth()
    {
        var purchase = NewPurchase(Day(2026, 10, 2), 100.00m);

        Assert.Equal(M(100.00m), Calc(YM(2026, 10), purchases: new[] { purchase }).CardInvoice);
        Assert.Equal(Money.Zero, Calc(YM(2026, 11), purchases: new[] { purchase }).CardInvoice);
    }

    [Fact]
    public void CardInvoice_IgnoresTheInstallmentsOfAPurchaseInProgressThatAreConsideredPaid()
    {
        var purchase = NewPurchase(Day(2026, 7, 10), 600.00m, count: 6, initial: 4);
        var purchases = new[] { purchase };

        Assert.Equal(Money.Zero, Calc(YM(2026, 8), purchases: purchases).CardInvoice);
        Assert.Equal(Money.Zero, Calc(YM(2026, 10), purchases: purchases).CardInvoice);
        Assert.Equal(M(100.00m), Calc(YM(2026, 11), purchases: purchases).CardInvoice);
    }

    // ---------- Result ----------

    [Fact]
    public void Result_IsIncomesMinusInvoiceDirectAndFixedExpenses()
    {
        var salary = NewIncome(Day(2026, 11, 5), 5000.00m);
        var pix = NewIncome(Day(2026, 11, 20), 200.00m, "Pix");
        var purchase = NewPurchase(Day(2026, 10, 10), 300.00m);
        var direct = NewDirect(Day(2026, 11, 15), 150.75m);
        var rent = NewFixed(YM(2026, 1), null, 1000.00m);

        var result = Calc(
            YM(2026, 11),
            incomes: new[] { salary, pix },
            direct: new[] { direct },
            fixedExpenses: new[] { rent },
            purchases: new[] { purchase });

        Assert.Equal(M(5200.00m), result.Incomes);
        Assert.Equal(M(300.00m), result.CardInvoice);
        Assert.Equal(M(150.75m), result.DirectExpenses);
        Assert.Equal(M(1000.00m), result.FixedExpenses);
        Assert.Equal(M(3749.25m), result.Result);
    }

    [Fact]
    public void Result_CanBeNegative()
    {
        var income = NewIncome(Day(2026, 11, 5), 100.00m);
        var rent = NewFixed(YM(2026, 1), null, 1000.00m);

        var result = Calc(YM(2026, 11), incomes: new[] { income }, fixedExpenses: new[] { rent });

        Assert.Equal(M(-900.00m), result.Result);
    }

    [Fact]
    public void Result_OnlyIncomes_IsTheIncomes()
    {
        var income = NewIncome(Day(2026, 11, 5), 321.45m);

        Assert.Equal(M(321.45m), Calc(YM(2026, 11), incomes: new[] { income }).Result);
    }

    // ---------- Accounts ----------

    [Fact]
    public void Calculate_IgnoresEveryItemOfOtherAccounts()
    {
        var income = NewIncome(Day(2026, 11, 5), 5000.00m, accountId: OtherAccountId);
        var direct = NewDirect(Day(2026, 11, 15), 100.00m, OtherAccountId);
        var rent = NewFixed(YM(2026, 1), null, 1000.00m, OtherAccountId);
        var purchase = NewPurchase(Day(2026, 10, 10), 300.00m, accountId: OtherAccountId);

        var result = Calc(
            YM(2026, 11),
            incomes: new[] { income },
            direct: new[] { direct },
            fixedExpenses: new[] { rent },
            purchases: new[] { purchase });

        Assert.Equal(Money.Zero, result.Incomes);
        Assert.Equal(Money.Zero, result.DirectExpenses);
        Assert.Equal(Money.Zero, result.FixedExpenses);
        Assert.Equal(Money.Zero, result.CardInvoice);
        Assert.Equal(Money.Zero, result.Result);
    }

    [Fact]
    public void Calculate_MixedAccounts_CountsOnlyTheRequestedOne()
    {
        var mine = NewIncome(Day(2026, 11, 5), 100.00m);
        var theirs = NewIncome(Day(2026, 11, 5), 900.00m, accountId: OtherAccountId);

        Assert.Equal(M(100.00m), Calc(YM(2026, 11), incomes: new[] { mine, theirs }).Incomes);
    }

    // ---------- Competence, not cash ----------

    [Fact]
    public void Calculate_UsesCompetenceDates_NotPaymentOrReceiptDates()
    {
        var income = NewIncome(Day(2026, 10, 28), 5000.00m);
        income.MarkAsReceived(Day(2026, 11, 2), Day(2026, 12, 31));
        var rent = NewFixed(YM(2026, 10), YM(2026, 10), 1000.00m);
        rent.MarkAsPaid(YM(2026, 10), Day(2026, 11, 2), Day(2026, 12, 31));

        var october = Calc(YM(2026, 10), incomes: new[] { income }, fixedExpenses: new[] { rent });
        var november = Calc(YM(2026, 11), incomes: new[] { income }, fixedExpenses: new[] { rent });

        Assert.Equal(M(4000.00m), october.Result);
        Assert.Equal(Money.Zero, november.Result);
    }

    // ---------- Arguments and stability ----------

    [Fact]
    public void Calculate_EmptyAccountId_Throws()
    {
        Assert.Throws<ArgumentException>(() => ProjectedResultCalculator.Calculate(
            Guid.Empty,
            YM(2026, 11),
            Array.Empty<Income>(),
            Array.Empty<DirectExpense>(),
            Array.Empty<FixedExpense>(),
            Array.Empty<CreditCardPurchase>()));
    }

    [Fact]
    public void Calculate_NullArguments_Throw()
    {
        var incomes = Array.Empty<Income>();
        var direct = Array.Empty<DirectExpense>();
        var fixedExpenses = Array.Empty<FixedExpense>();
        var purchases = Array.Empty<CreditCardPurchase>();

        Assert.Throws<ArgumentNullException>(() =>
            ProjectedResultCalculator.Calculate(AccountId, null!, incomes, direct, fixedExpenses, purchases));
        Assert.Throws<ArgumentNullException>(() =>
            ProjectedResultCalculator.Calculate(AccountId, YM(2026, 11), null!, direct, fixedExpenses, purchases));
        Assert.Throws<ArgumentNullException>(() =>
            ProjectedResultCalculator.Calculate(AccountId, YM(2026, 11), incomes, null!, fixedExpenses, purchases));
        Assert.Throws<ArgumentNullException>(() =>
            ProjectedResultCalculator.Calculate(AccountId, YM(2026, 11), incomes, direct, null!, purchases));
        Assert.Throws<ArgumentNullException>(() =>
            ProjectedResultCalculator.Calculate(AccountId, YM(2026, 11), incomes, direct, fixedExpenses, null!));
    }

    [Fact]
    public void Calculate_WithConditionId_FiltersOnlyCardInvoiceAndPreservesAccrualResult()
    {
        var salary = NewIncome(Day(2026, 11, 5), 5000.00m);
        var direct = NewDirect(Day(2026, 11, 15), 200.00m);
        var rent = NewFixed(YM(2026, 1), null, 1000.00m);
        var ownPurchase = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, CardId, Day(2026, 10, 10), M(300.00m), 1, 1,
            Rule3, SystemPurchaseConditions.OwnId, "Meu", Guid.NewGuid());
        var othersPurchase = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, CardId, Day(2026, 10, 12), M(700.00m), 1, 1,
            Rule3, SystemPurchaseConditions.OthersId, "Terceiro", Guid.NewGuid());

        var unfiltered = ProjectedResultCalculator.Calculate(
            AccountId, YM(2026, 11), [salary], [direct], [rent], [ownPurchase, othersPurchase]);
        var ownOnly = ProjectedResultCalculator.Calculate(
            AccountId, YM(2026, 11), [salary], [direct], [rent], [ownPurchase, othersPurchase], SystemPurchaseConditions.OwnId);

        Assert.Null(unfiltered.ConditionId);
        Assert.Equal(M(1000.00m), unfiltered.CardInvoice);
        Assert.Equal(M(2800.00m), unfiltered.Result);

        Assert.Equal(SystemPurchaseConditions.OwnId, ownOnly.ConditionId);
        Assert.Equal(M(5000.00m), ownOnly.Incomes);
        Assert.Equal(M(200.00m), ownOnly.DirectExpenses);
        Assert.Equal(M(1000.00m), ownOnly.FixedExpenses);
        Assert.Equal(M(300.00m), ownOnly.CardInvoice);
        Assert.Equal(M(3500.00m), ownOnly.Result);

        Assert.Throws<ArgumentException>(() => ProjectedResultCalculator.Calculate(
            AccountId, YM(2026, 11), [], [], [], [], Guid.Empty));
    }

    [Fact]
    public void Calculate_CalledTwice_ReturnsEqualResults_AndDoesNotChangeTheEntities()
    {
        var income = NewIncome(Day(2026, 11, 5), 5000.00m);
        var rent = NewFixed(YM(2026, 1), null, 1000.00m);
        var purchase = NewPurchase(Day(2026, 10, 10), 300.00m, count: 3);

        var first = Calc(YM(2026, 11), incomes: new[] { income }, fixedExpenses: new[] { rent }, purchases: new[] { purchase });
        var second = Calc(YM(2026, 11), incomes: new[] { income }, fixedExpenses: new[] { rent }, purchases: new[] { purchase });

        Assert.Equal(first, second);
        Assert.False(income.IsReceived);
        Assert.Empty(rent.Payments);
        Assert.Single(rent.AmountHistory);
        Assert.Equal(M(300.00m), purchase.Amount);
    }

    // ---------- Main invariants ----------

    private static DateOnly RandomDate(Random random) => Day(2026, 1, 1).AddDays(random.Next(0, 700));

    private static decimal RandomAmount(Random random) => random.Next(1, 500000) / 100m;

    [Fact]
    public void EachMonth_MatchesAnIndependentCalculation_NoMatterTheOrderOrThePaymentState()
    {
        var random = new Random(20261030);

        var incomes = new List<Income>();
        var directs = new List<DirectExpense>();
        var fixeds = new List<FixedExpense>();
        var purchases = new List<CreditCardPurchase>();

        for (var i = 0; i < 120; i++)
        {
            var accountId = random.Next(4) == 0 ? OtherAccountId : AccountId;

            var income = NewIncome(RandomDate(random), RandomAmount(random), accountId: accountId);
            if (random.Next(2) == 0)
            {
                income.MarkAsReceived(RandomDate(random), Day(2028, 12, 31));
            }

            incomes.Add(income);

            directs.Add(NewDirect(RandomDate(random), RandomAmount(random), accountId));

            var start = YM(2026, 1).AddMonths(random.Next(0, 20));
            YearMonth? end = random.Next(2) == 0 ? null : start.AddMonths(random.Next(0, 25));
            var fixedExpense = NewFixed(start, end, RandomAmount(random), accountId);
            if (random.Next(2) == 0)
            {
                var changeDate = start.AddMonths(random.Next(1, 6)).FirstDay;
                var effective = FixedExpense.FirstOpenMonth(changeDate);
                if (end is null || effective <= end)
                {
                    fixedExpense.ChangeAmount(M(RandomAmount(random)), changeDate);
                }
            }

            if (random.Next(2) == 0)
            {
                fixedExpense.MarkAsPaid(start, RandomDate(random), Day(2028, 12, 31));
            }

            fixeds.Add(fixedExpense);

            var count = random.Next(1, 25);
            var initial = random.Next(1, count + 1);
            var rule = CardBillingRule.Of(random.Next(1, 29), random.Next(1, 29));
            purchases.Add(NewPurchase(
                RandomDate(random),
                random.Next(count, 200000) / 100m,
                count,
                initial,
                accountId,
                rule));
        }

        var reversedIncomes = incomes.AsEnumerable().Reverse().ToList();
        var reversedDirects = directs.AsEnumerable().Reverse().ToList();
        var reversedFixeds = fixeds.AsEnumerable().Reverse().ToList();
        var reversedPurchases = purchases.AsEnumerable().Reverse().ToList();

        var sumOfIncomes = Money.Zero;
        var sumOfDirects = Money.Zero;

        for (var month = YM(2025, 12); month <= YM(2030, 12); month = month.AddMonths(1))
        {
            var expectedIncomes = Money.Zero;
            foreach (var income in incomes.Where(x => x.AccountId == AccountId))
            {
                if (YearMonth.From(income.ExpectedDate) == month)
                {
                    expectedIncomes += income.Amount;
                }
            }

            var expectedDirects = Money.Zero;
            foreach (var direct in directs.Where(x => x.AccountId == AccountId))
            {
                if (YearMonth.From(direct.Date) == month)
                {
                    expectedDirects += direct.Amount;
                }
            }

            var expectedFixed = Money.Zero;
            foreach (var fixedExpense in fixeds.Where(x => x.AccountId == AccountId))
            {
                var active = month >= fixedExpense.StartMonth
                             && (fixedExpense.EndMonth is null || month <= fixedExpense.EndMonth);
                if (active)
                {
                    expectedFixed += fixedExpense.AmountHistory
                        .Last(change => change.EffectiveFrom <= month)
                        .Amount;
                }
            }

            var expectedInvoice = Money.Zero;
            foreach (var purchase in purchases.Where(x => x.AccountId == AccountId))
            {
                var amounts = purchase.Amount.Split(purchase.InstallmentCount);
                var firstClosing = BillingCycle.ForPurchase(purchase.Date, purchase.Rule).ClosingMonth;

                for (var k = purchase.InitialInstallment - 1; k < purchase.InstallmentCount; k++)
                {
                    if (firstClosing.AddMonths(k) == month)
                    {
                        expectedInvoice += amounts[k];
                    }
                }
            }

            var result = Calc(month, incomes, directs, fixeds, purchases);
            var reversed = Calc(month, reversedIncomes, reversedDirects, reversedFixeds, reversedPurchases);
            var context = $"month={month.Year}-{month.Month:00}";

            Assert.True(result.Incomes == expectedIncomes, $"Incomes differ: {context}");
            Assert.True(result.DirectExpenses == expectedDirects, $"Direct expenses differ: {context}");
            Assert.True(result.FixedExpenses == expectedFixed, $"Fixed expenses differ: {context}");
            Assert.True(result.CardInvoice == expectedInvoice, $"Card invoice differs: {context}");
            Assert.True(
                result.Result == expectedIncomes - expectedInvoice - expectedDirects - expectedFixed,
                $"Result differs from the formula: {context}");
            Assert.True(result == reversed, $"The result depends on the order of the items: {context}");

            sumOfIncomes += result.Incomes;
            sumOfDirects += result.DirectExpenses;
        }

        // Every income and every direct expense of the account is counted in exactly one month.
        var allIncomes = Money.Sum(incomes.Where(x => x.AccountId == AccountId).Select(x => x.Amount));
        var allDirects = Money.Sum(directs.Where(x => x.AccountId == AccountId).Select(x => x.Amount));

        Assert.Equal(allIncomes, sumOfIncomes);
        Assert.Equal(allDirects, sumOfDirects);
    }
}