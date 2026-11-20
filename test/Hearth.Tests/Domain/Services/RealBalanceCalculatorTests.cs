using Hearth.Domain.Entities;
using Hearth.Domain.Services;
using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.Services;

public class RealBalanceCalculatorTests
{
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid OtherAccountId = Guid.NewGuid();
    private static readonly CardBillingRule Rule3 = CardBillingRule.Of(3, 12);
    private static readonly DateOnly InitialDate = new(2026, 10, 1);

    private static Money M(decimal amount) => Money.FromDecimal(amount);

    private static YearMonth YM(int year, int month) => YearMonth.Of(year, month);

    private static DateOnly Day(int year, int month, int day) => new(year, month, day);

    private static Account NewAccount(decimal initialBalance = 1000.00m, Guid? id = null) =>
        Account.Create(id ?? AccountId, "Conta", M(initialBalance), InitialDate, Rule3);

    private static Income NewIncome(
        DateOnly expectedDate,
        decimal amount,
        DateOnly? receivedDate = null,
        Guid? accountId = null)
    {
        var income = Income.Create(
            Guid.NewGuid(), accountId ?? AccountId, expectedDate, M(amount), "Salário");

        if (receivedDate.HasValue)
        {
            income.MarkAsReceived(receivedDate.Value);
        }

        return income;
    }

    private static DirectExpense NewDirect(DateOnly date, decimal amount, Guid? accountId = null) =>
        DirectExpense.Create(
            Guid.NewGuid(), accountId ?? AccountId, date, M(amount), "Mercado", Guid.NewGuid());

    private static FixedExpense NewFixed(
        YearMonth start, YearMonth? end, decimal amount, Guid? accountId = null) =>
        FixedExpense.Create(
            Guid.NewGuid(), accountId ?? AccountId, start, end, M(amount), "Aluguel", Guid.NewGuid());

    private static Invoice NewInvoice(
        BillingCycle cycle,
        decimal chargedAmount,
        DateOnly? paidOn = null,
        Guid? accountId = null)
    {
        var invoice = Invoice.Create(Guid.NewGuid(), accountId ?? AccountId, cycle);
        invoice.InformChargedAmount(M(chargedAmount));

        if (paidOn.HasValue)
        {
            invoice.MarkAsPaid(paidOn.Value);
        }

        return invoice;
    }

    private static RealBalance Calc(
        Account account,
        DateOnly until,
        IEnumerable<Income>? incomes = null,
        IEnumerable<DirectExpense>? direct = null,
        IEnumerable<FixedExpense>? fixedExpenses = null,
        IEnumerable<Invoice>? invoices = null) =>
        RealBalanceCalculator.Calculate(
            account,
            until,
            incomes ?? Array.Empty<Income>(),
            direct ?? Array.Empty<DirectExpense>(),
            fixedExpenses ?? Array.Empty<FixedExpense>(),
            invoices ?? Array.Empty<Invoice>());

    // ---------- Empty ----------

    [Fact]
    public void Calculate_NoItems_EqualsTheInitialBalance()
    {
        var account = NewAccount(initialBalance: 5000.00m);

        var balance = Calc(account, Day(2026, 10, 31));

        Assert.Equal(Day(2026, 10, 31), balance.Until);
        Assert.Equal(M(5000.00m), balance.InitialBalance);
        Assert.Equal(Money.Zero, balance.Incomes);
        Assert.Equal(Money.Zero, balance.DirectExpenses);
        Assert.Equal(Money.Zero, balance.FixedExpenses);
        Assert.Equal(Money.Zero, balance.CardInvoices);
        Assert.Equal(M(5000.00m), balance.Balance);
    }

    [Fact]
    public void Calculate_InitialBalanceCanBeZeroOrNegative()
    {
        var zero = NewAccount(initialBalance: 0m);
        var negative = NewAccount(initialBalance: -500.00m);

        Assert.Equal(Money.Zero, Calc(zero, Day(2026, 10, 31)).Balance);
        Assert.Equal(M(-500.00m), Calc(negative, Day(2026, 10, 31)).Balance);
    }

    // ---------- Incomes ----------

    [Fact]
    public void Incomes_CountByTheReceivedDate()
    {
        var expected = NewIncome(Day(2026, 10, 28), 5000.00m);
        var received = NewIncome(Day(2026, 10, 28), 5000.00m, receivedDate: Day(2026, 11, 2));
        var account = NewAccount();

        Assert.Equal(Money.Zero, Calc(account, Day(2026, 10, 31), incomes: new[] { expected }).Incomes);
        Assert.Equal(Money.Zero, Calc(account, Day(2026, 10, 31), incomes: new[] { received }).Incomes);
        Assert.Equal(M(5000.00m), Calc(account, Day(2026, 11, 2), incomes: new[] { received }).Incomes);
    }

    [Fact]
    public void Incomes_FirstAndLastDay_InclusiveOnBothEnds()
    {
        var first = NewIncome(Day(2026, 10, 1), 100.00m, receivedDate: Day(2026, 10, 1));
        var last = NewIncome(Day(2026, 10, 31), 200.00m, receivedDate: Day(2026, 10, 31));
        var after = NewIncome(Day(2026, 11, 1), 400.00m, receivedDate: Day(2026, 11, 1));
        var account = NewAccount();

        var result = Calc(account, Day(2026, 10, 31), incomes: new[] { first, last, after });

        Assert.Equal(M(300.00m), result.Incomes);
    }

    [Fact]
    public void Incomes_OnlyReceivedCountInTheCash()
    {
        var received = NewIncome(Day(2026, 10, 5), 100.00m, receivedDate: Day(2026, 10, 10));
        var unreceived = NewIncome(Day(2026, 10, 15), 100.00m);
        var account = NewAccount();

        var result = Calc(account, Day(2026, 10, 31), incomes: new[] { received, unreceived });

        Assert.Equal(M(100.00m), result.Incomes);
    }

    [Fact]
    public void Incomes_ExpectedDateDoesNotMatter_OnlyReceivedDate()
    {
        var income = NewIncome(Day(2026, 12, 25), 5000.00m, receivedDate: Day(2026, 10, 5));
        var account = NewAccount();

        Assert.Equal(M(5000.00m), Calc(account, Day(2026, 10, 31), incomes: new[] { income }).Incomes);
        Assert.Equal(M(5000.00m), Calc(account, Day(2026, 11, 30), incomes: new[] { income }).Incomes);
    }

    // ---------- Direct expenses ----------

    [Fact]
    public void DirectExpenses_CountByTheirDate()
    {
        var first = NewDirect(Day(2026, 10, 1), 10.00m);
        var middle = NewDirect(Day(2026, 10, 15), 20.00m);
        var last = NewDirect(Day(2026, 10, 31), 40.00m);
        var before = NewDirect(Day(2026, 9, 30), 80.00m);
        var after = NewDirect(Day(2026, 11, 1), 160.00m);
        var account = NewAccount();

        var result = Calc(account, Day(2026, 10, 31), direct: new[] { first, middle, last, before, after });

        Assert.Equal(M(70.00m), result.DirectExpenses);
    }

    [Fact]
    public void DirectExpenses_AllDatesBetweenFirstAndLastDay_InclusiveOnBothEnds()
    {
        var first = NewDirect(Day(2026, 10, 1), 100.00m);
        var before = NewDirect(Day(2026, 9, 30), 10.00m);
        var after = NewDirect(Day(2026, 11, 1), 20.00m);
        var account = NewAccount();

        var result = Calc(account, Day(2026, 10, 1), direct: new[] { first, before, after });

        Assert.Equal(M(100.00m), result.DirectExpenses);
    }

    // ---------- Fixed expenses ----------

    [Fact]
    public void FixedExpenses_CountByThePaymentDate_AtTheAmountOfTheMonthPaid()
    {
        var expense = NewFixed(YM(2026, 1), null, 1000.00m);
        var account = NewAccount();

        var unpaid = Calc(account, Day(2026, 10, 31), fixedExpenses: new[] { expense });
        Assert.Equal(Money.Zero, unpaid.FixedExpenses);

        expense.MarkAsPaid(YM(2026, 10), Day(2026, 10, 15));

        var paid = Calc(account, Day(2026, 10, 31), fixedExpenses: new[] { expense });
        Assert.Equal(M(1000.00m), paid.FixedExpenses);
    }

    [Fact]
    public void FixedExpenses_PaidInMultipleMonths_EachCountsWithItsOwnAmount()
    {
        var expense = NewFixed(YM(2026, 1), null, 1000.00m);
        expense.MarkAsPaid(YM(2026, 10), Day(2026, 10, 5));
        // Changed on 10/10 (after the closing day 3), so the new amount applies from November.
        expense.ChangeAmount(M(1200.00m), Day(2026, 10, 10), Rule3);
        expense.MarkAsPaid(YM(2026, 11), Day(2026, 11, 5));
        var account = NewAccount();

        var october = Calc(account, Day(2026, 10, 31), fixedExpenses: new[] { expense });
        var november = Calc(account, Day(2026, 11, 30), fixedExpenses: new[] { expense });

        Assert.Equal(M(1000.00m), october.FixedExpenses);
        Assert.Equal(M(2200.00m), november.FixedExpenses);
    }

    [Fact]
    public void FixedExpenses_PaymentAndCompetenceInDifferentMonths_CountsByPaymentDate()
    {
        // October's expense paid on 02/11: the cash leaves in November, so it counts on 02/11.
        var expense = NewFixed(YM(2026, 10), YM(2026, 10), 1000.00m);
        expense.MarkAsPaid(YM(2026, 10), Day(2026, 11, 2));
        var account = NewAccount();

        var october = Calc(account, Day(2026, 10, 31), fixedExpenses: new[] { expense });
        var november = Calc(account, Day(2026, 11, 2), fixedExpenses: new[] { expense });

        Assert.Equal(Money.Zero, october.FixedExpenses);
        Assert.Equal(M(1000.00m), november.FixedExpenses);
    }

    [Fact]
    public void FixedExpenses_FirstAndLastDay_Inclusive()
    {
        var expense = NewFixed(YM(2026, 1), null, 1000.00m);
        expense.MarkAsPaid(YM(2026, 10), Day(2026, 10, 1));
        var account = NewAccount();

        var result = Calc(account, Day(2026, 10, 1), fixedExpenses: new[] { expense });

        Assert.Equal(M(1000.00m), result.FixedExpenses);
    }

    // ---------- Card invoices ----------

    [Fact]
    public void CardInvoices_CountByThePaymentDate_AtTheChargedAmount()
    {
        var cycle = BillingCycle.Closing(YM(2026, 11), Rule3);
        var invoice = NewInvoice(cycle, chargedAmount: 500.00m, paidOn: Day(2026, 11, 15));
        var account = NewAccount();

        var before = Calc(account, Day(2026, 11, 14), invoices: new[] { invoice });
        var onDay = Calc(account, Day(2026, 11, 15), invoices: new[] { invoice });
        var endOfMonth = Calc(account, Day(2026, 11, 30), invoices: new[] { invoice });

        Assert.Equal(Money.Zero, before.CardInvoices);
        Assert.Equal(M(500.00m), onDay.CardInvoices);
        Assert.Equal(M(500.00m), endOfMonth.CardInvoices);
    }

    [Fact]
    public void CardInvoices_UnpaidDoNotCount()
    {
        var cycle = BillingCycle.Closing(YM(2026, 11), Rule3);
        var unpaid = NewInvoice(cycle, chargedAmount: 500.00m);
        var account = NewAccount();

        var result = Calc(account, Day(2026, 11, 30), invoices: new[] { unpaid });

        Assert.Equal(Money.Zero, result.CardInvoices);
    }

    [Fact]
    public void CardInvoices_DueBeforePaid_TakesThePaymentDate()
    {
        // November invoice due on 12/11, paid on 02/12: the cash leaves in December.
        var cycle = BillingCycle.Closing(YM(2026, 11), Rule3);
        var invoice = NewInvoice(cycle, chargedAmount: 500.00m, paidOn: Day(2026, 12, 2));
        var account = NewAccount();

        var november = Calc(account, Day(2026, 11, 30), invoices: new[] { invoice });
        var december = Calc(account, Day(2026, 12, 2), invoices: new[] { invoice });

        Assert.Equal(Money.Zero, november.CardInvoices);
        Assert.Equal(M(500.00m), december.CardInvoices);
    }

    [Fact]
    public void CardInvoices_UsesTheChargedAmount_NotTheCalculated()
    {
        var cycle = BillingCycle.Closing(YM(2026, 11), Rule3);
        var invoice = NewInvoice(cycle, chargedAmount: 520.50m, paidOn: Day(2026, 11, 15));
        var account = NewAccount();

        var result = Calc(account, Day(2026, 11, 30), invoices: new[] { invoice });

        Assert.Equal(M(520.50m), result.CardInvoices);
    }

    [Fact]
    public void CardInvoices_FirstAndLastDay_Inclusive()
    {
        var first = NewInvoice(
            BillingCycle.Closing(YM(2026, 10), Rule3), chargedAmount: 100.00m, paidOn: Day(2026, 11, 1));
        var last = NewInvoice(
            BillingCycle.Closing(YM(2026, 11), Rule3), chargedAmount: 200.00m, paidOn: Day(2026, 11, 30));
        var after = NewInvoice(
            BillingCycle.Closing(YM(2026, 12), Rule3), chargedAmount: 400.00m, paidOn: Day(2026, 12, 1));
        var account = NewAccount();

        var result = Calc(account, Day(2026, 11, 30), invoices: new[] { first, last, after });

        Assert.Equal(M(300.00m), result.CardInvoices);
    }

    // ---------- Initial balance date ----------

    [Fact]
    public void Calculate_IgnoresItemsBeforeTheInitialBalanceDate()
    {
        var income = NewIncome(Day(2026, 9, 15), 1000.00m, receivedDate: Day(2026, 9, 20));
        var direct = NewDirect(Day(2026, 9, 20), 100.00m);
        var expense = NewFixed(YM(2026, 1), null, 1000.00m);
        expense.MarkAsPaid(YM(2026, 9), Day(2026, 9, 20));
        var invoice = NewInvoice(
            BillingCycle.Closing(YM(2026, 9), Rule3), chargedAmount: 500.00m, paidOn: Day(2026, 9, 20));
        var account = NewAccount();

        var result = Calc(
            account,
            Day(2026, 12, 31),
            incomes: new[] { income },
            direct: new[] { direct },
            fixedExpenses: new[] { expense },
            invoices: new[] { invoice });

        Assert.Equal(Money.Zero, result.Incomes);
        Assert.Equal(Money.Zero, result.DirectExpenses);
        Assert.Equal(Money.Zero, result.FixedExpenses);
        Assert.Equal(Money.Zero, result.CardInvoices);
        Assert.Equal(account.InitialBalance, result.Balance);
    }

    [Fact]
    public void Calculate_IncludesItemsOnTheInitialBalanceDate()
    {
        var income = NewIncome(InitialDate, 1000.00m, receivedDate: InitialDate);
        var direct = NewDirect(InitialDate, 100.00m);
        var expense = NewFixed(YM(2026, 10), null, 1000.00m);
        expense.MarkAsPaid(YM(2026, 10), InitialDate);
        var invoice = NewInvoice(
            BillingCycle.Closing(YM(2026, 10), Rule3), chargedAmount: 500.00m, paidOn: InitialDate);
        var account = NewAccount();

        var result = Calc(
            account,
            InitialDate,
            incomes: new[] { income },
            direct: new[] { direct },
            fixedExpenses: new[] { expense },
            invoices: new[] { invoice });

        Assert.Equal(M(1000.00m), result.Incomes);
        Assert.Equal(M(100.00m), result.DirectExpenses);
        Assert.Equal(M(1000.00m), result.FixedExpenses);
        Assert.Equal(M(500.00m), result.CardInvoices);
    }

    [Fact]
    public void Calculate_BeforeTheInitialBalanceDate_EqualsTheInitialBalance()
    {
        var income = NewIncome(Day(2026, 9, 15), 1000.00m, receivedDate: Day(2026, 9, 20));
        var account = NewAccount(initialBalance: 5000.00m);

        var result = Calc(account, InitialDate.AddDays(-1), incomes: new[] { income });

        Assert.Equal(M(5000.00m), result.Balance);
        Assert.Equal(Money.Zero, result.Incomes);
        Assert.Equal(Money.Zero, result.DirectExpenses);
        Assert.Equal(Money.Zero, result.FixedExpenses);
        Assert.Equal(Money.Zero, result.CardInvoices);
    }

    // ---------- Accounts ----------

    [Fact]
    public void Calculate_IgnoresEveryItemOfOtherAccounts()
    {
        var income = NewIncome(Day(2026, 10, 10), 1000.00m, receivedDate: Day(2026, 10, 10), accountId: OtherAccountId);
        var direct = NewDirect(Day(2026, 10, 10), 100.00m, OtherAccountId);
        var expense = NewFixed(YM(2026, 10), null, 1000.00m, OtherAccountId);
        expense.MarkAsPaid(YM(2026, 10), Day(2026, 10, 10));
        var cycle = BillingCycle.Closing(YM(2026, 10), Rule3);
        var invoice = NewInvoice(cycle, chargedAmount: 500.00m, paidOn: Day(2026, 10, 10), accountId: OtherAccountId);
        var account = NewAccount();

        var result = Calc(
            account,
            Day(2026, 10, 31),
            incomes: new[] { income },
            direct: new[] { direct },
            fixedExpenses: new[] { expense },
            invoices: new[] { invoice });

        Assert.Equal(Money.Zero, result.Incomes);
        Assert.Equal(Money.Zero, result.DirectExpenses);
        Assert.Equal(Money.Zero, result.FixedExpenses);
        Assert.Equal(Money.Zero, result.CardInvoices);
        Assert.Equal(M(1000.00m), result.Balance);
    }

    // ---------- Balance formula ----------

    [Fact]
    public void Balance_IsInitialPlusIncomesMinusExpensesAndInvoices()
    {
        var income = NewIncome(Day(2026, 10, 10), 5000.00m, receivedDate: Day(2026, 10, 10));
        var direct = NewDirect(Day(2026, 10, 15), 150.75m);
        var expense = NewFixed(YM(2026, 10), null, 1000.00m);
        expense.MarkAsPaid(YM(2026, 10), Day(2026, 10, 20));
        var cycle = BillingCycle.Closing(YM(2026, 10), Rule3);
        var invoice = NewInvoice(cycle, chargedAmount: 520.50m, paidOn: Day(2026, 10, 25));
        var account = NewAccount(initialBalance: 2000.00m);

        var result = Calc(
            account,
            Day(2026, 10, 31),
            incomes: new[] { income },
            direct: new[] { direct },
            fixedExpenses: new[] { expense },
            invoices: new[] { invoice });

        Assert.Equal(M(2000.00m), result.InitialBalance);
        Assert.Equal(M(5000.00m), result.Incomes);
        Assert.Equal(M(150.75m), result.DirectExpenses);
        Assert.Equal(M(1000.00m), result.FixedExpenses);
        Assert.Equal(M(520.50m), result.CardInvoices);
        Assert.Equal(M(5328.75m), result.Balance);
    }

    [Fact]
    public void Balance_CanBeNegative()
    {
        var direct = NewDirect(Day(2026, 10, 10), 2000.00m);
        var account = NewAccount(initialBalance: 500.00m);

        var result = Calc(account, Day(2026, 10, 31), direct: new[] { direct });

        Assert.Equal(M(-1500.00m), result.Balance);
    }

    // ---------- Arguments and stability ----------

    [Fact]
    public void Calculate_NullAccount_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => RealBalanceCalculator.Calculate(
            null!,
            Day(2026, 10, 31),
            Array.Empty<Income>(),
            Array.Empty<DirectExpense>(),
            Array.Empty<FixedExpense>(),
            Array.Empty<Invoice>()));
    }

    [Fact]
    public void Calculate_NullCollections_Throw()
    {
        var account = NewAccount();

        Assert.Throws<ArgumentNullException>(() => RealBalanceCalculator.Calculate(
            account,
            Day(2026, 10, 31),
            null!,
            Array.Empty<DirectExpense>(),
            Array.Empty<FixedExpense>(),
            Array.Empty<Invoice>()));
        Assert.Throws<ArgumentNullException>(() => RealBalanceCalculator.Calculate(
            account,
            Day(2026, 10, 31),
            Array.Empty<Income>(),
            null!,
            Array.Empty<FixedExpense>(),
            Array.Empty<Invoice>()));
        Assert.Throws<ArgumentNullException>(() => RealBalanceCalculator.Calculate(
            account,
            Day(2026, 10, 31),
            Array.Empty<Income>(),
            Array.Empty<DirectExpense>(),
            null!,
            Array.Empty<Invoice>()));
        Assert.Throws<ArgumentNullException>(() => RealBalanceCalculator.Calculate(
            account,
            Day(2026, 10, 31),
            Array.Empty<Income>(),
            Array.Empty<DirectExpense>(),
            Array.Empty<FixedExpense>(),
            null!));
    }

    [Fact]
    public void Calculate_CalledTwice_ReturnsEqualResults_AndDoesNotChangeTheEntities()
    {
        var income = NewIncome(Day(2026, 10, 10), 5000.00m, receivedDate: Day(2026, 10, 10));
        var direct = NewDirect(Day(2026, 10, 15), 150.00m);
        var expense = NewFixed(YM(2026, 10), null, 1000.00m);
        expense.MarkAsPaid(YM(2026, 10), Day(2026, 10, 20));
        var cycle = BillingCycle.Closing(YM(2026, 10), Rule3);
        var invoice = NewInvoice(cycle, chargedAmount: 520.00m, paidOn: Day(2026, 10, 25));
        var account = NewAccount();

        var first = Calc(
            account, Day(2026, 10, 31), new[] { income }, new[] { direct }, new[] { expense }, new[] { invoice });
        var second = Calc(
            account, Day(2026, 10, 31), new[] { income }, new[] { direct }, new[] { expense }, new[] { invoice });

        Assert.Equal(first, second);
        Assert.True(income.IsReceived);
        Assert.Equal(Day(2026, 10, 10), income.ReceivedOn);
        Assert.Single(expense.Payments);
        Assert.True(invoice.IsPaid);
        Assert.Equal(Day(2026, 10, 25), invoice.PaidOn);
        Assert.Equal(M(520.00m), invoice.ChargedAmount);
    }

    // ---------- Main invariants ----------

    private static DateOnly RandomDate(Random random) => InitialDate.AddDays(random.Next(-60, 700));

    private static decimal RandomAmount(Random random) => random.Next(1, 500000) / 100m;

    [Fact]
    public void EachDay_MatchesAnIndependentCalculation_NoMatterTheOrderOrTheReceiptState()
    {
        var random = new Random(20261031);
        var account = NewAccount(initialBalance: random.Next(-50000, 150000) / 100m);

        var incomes = new List<Income>();
        var directs = new List<DirectExpense>();
        var fixeds = new List<FixedExpense>();
        var invoices = new List<Invoice>();

        for (var i = 0; i < 100; i++)
        {
            var accountId = random.Next(4) == 0 ? OtherAccountId : AccountId;

            var income = NewIncome(RandomDate(random), RandomAmount(random), accountId: accountId);
            if (random.Next(2) == 0)
            {
                income.MarkAsReceived(RandomDate(random));
            }

            incomes.Add(income);

            directs.Add(NewDirect(RandomDate(random), RandomAmount(random), accountId));

            var start = YM(2026, 1).AddMonths(random.Next(0, 20));
            var duration = random.Next(0, 25);
            YearMonth? end = random.Next(2) == 0 ? null : start.AddMonths(duration);
            var fixedExpense = NewFixed(start, end, RandomAmount(random), accountId);
            if (random.Next(2) == 0)
            {
                var changeDate = start.AddMonths(random.Next(1, 6)).FirstDay;
                var changeRule = CardBillingRule.Of(random.Next(1, 29), random.Next(1, 29));
                var effective = FixedExpense.FirstOpenMonth(changeDate, changeRule);
                if (end is null || effective <= end)
                {
                    fixedExpense.ChangeAmount(M(RandomAmount(random)), changeDate, changeRule);
                }
            }

            var maxOffset = end is null ? 24 : duration;
            var paymentCount = random.Next(1, 4);
            for (var p = 0; p < paymentCount; p++)
            {
                var month = start.AddMonths(random.Next(0, maxOffset + 1));
                fixedExpense.MarkAsPaid(month, RandomDate(random));
            }

            fixeds.Add(fixedExpense);

            var cycle = BillingCycle.ForPurchase(
                RandomDate(random), CardBillingRule.Of(random.Next(1, 29), random.Next(1, 29)));
            var invoice = NewInvoice(cycle, RandomAmount(random), accountId: accountId);
            if (random.Next(2) == 0)
            {
                invoice.MarkAsPaid(RandomDate(random));
            }

            invoices.Add(invoice);
        }

        var reversedIncomes = incomes.AsEnumerable().Reverse().ToList();
        var reversedDirects = directs.AsEnumerable().Reverse().ToList();
        var reversedFixeds = fixeds.AsEnumerable().Reverse().ToList();
        var reversedInvoices = invoices.AsEnumerable().Reverse().ToList();

        for (var date = InitialDate.AddDays(-10); date <= InitialDate.AddDays(730); date = date.AddDays(1))
        {
            var expectedIncomes = Money.Zero;
            foreach (var income in incomes.Where(x => x.AccountId == account.Id))
            {
                if (income.ReceivedOn is { } received && received >= InitialDate && received <= date)
                {
                    expectedIncomes += income.Amount;
                }
            }

            var expectedDirects = Money.Zero;
            foreach (var direct in directs.Where(x => x.AccountId == account.Id))
            {
                if (direct.Date >= InitialDate && direct.Date <= date)
                {
                    expectedDirects += direct.Amount;
                }
            }

            var expectedFixeds = Money.Zero;
            foreach (var fixedExpense in fixeds.Where(x => x.AccountId == account.Id))
            {
                foreach (var payment in fixedExpense.Payments)
                {
                    if (payment.Value >= InitialDate && payment.Value <= date)
                    {
                        expectedFixeds += fixedExpense.AmountIn(payment.Key) ?? Money.Zero;
                    }
                }
            }

            var expectedInvoices = Money.Zero;
            foreach (var invoice in invoices.Where(x => x.AccountId == account.Id))
            {
                if (invoice.PaidOn is { } paidOn && paidOn >= InitialDate && paidOn <= date)
                {
                    expectedInvoices += invoice.ChargedAmount ?? Money.Zero;
                }
            }

            var result = RealBalanceCalculator.Calculate(account, date, incomes, directs, fixeds, invoices);
            var reversedResult = RealBalanceCalculator.Calculate(
                account, date, reversedIncomes, reversedDirects, reversedFixeds, reversedInvoices);
            var context = $"date={date:yyyy-MM-dd}";

            Assert.True(result.Incomes == expectedIncomes, $"Incomes differ: {context}");
            Assert.True(result.DirectExpenses == expectedDirects, $"Direct expenses differ: {context}");
            Assert.True(result.FixedExpenses == expectedFixeds, $"Fixed expenses differ: {context}");
            Assert.True(result.CardInvoices == expectedInvoices, $"Card invoices differ: {context}");
            Assert.True(
                result.Balance == account.InitialBalance + expectedIncomes - expectedDirects - expectedFixeds - expectedInvoices,
                $"Balance differs from the formula: {context}");
            Assert.True(result == reversedResult, $"The result depends on the order of the items: {context}");
        }
    }
}