using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.Services;
using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.Services;

public class RealBalanceCalculatorTests
{
    private static readonly Guid OwnerUserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid OtherAccountId = Guid.NewGuid();
    private static readonly Guid CardId = Guid.NewGuid();
    private static readonly Guid ArchivedCardId = Guid.NewGuid();
    private static readonly CardBillingRule Rule3_12 = CardBillingRule.Of(3, 12);
    private static readonly CardBillingRule Rule25_5 = CardBillingRule.Of(25, 5);
    private static readonly DateOnly InitialDate = new(2026, 10, 1);

    private static Money M(decimal amount) => Money.FromDecimal(amount);

    private static Account NewAccount(decimal initialBalance = 1000.00m, Guid? id = null) =>
        Account.Create(id ?? AccountId, OwnerUserId, "Conta", M(initialBalance), InitialDate);

    [Fact]
    public void Calculate_NullArguments_Throw()
    {
        var account = NewAccount();
        var until = new DateOnly(2026, 11, 20);

        Assert.Throws<ArgumentNullException>(() => RealBalanceCalculator.Calculate(null!, until, [], [], [], []));
        Assert.Throws<ArgumentNullException>(() => RealBalanceCalculator.Calculate(account, until, null!, [], [], []));
        Assert.Throws<ArgumentNullException>(() => RealBalanceCalculator.Calculate(account, until, [], null!, [], []));
        Assert.Throws<ArgumentNullException>(() => RealBalanceCalculator.Calculate(account, until, [], [], null!, []));
        Assert.Throws<ArgumentNullException>(() => RealBalanceCalculator.Calculate(account, until, [], [], [], null!));
    }

    [Fact]
    public void Calculate_WithEmptyCollections_ReturnsInitialBalanceAndZeroComponents()
    {
        var account = NewAccount(initialBalance: -250.50m);
        var until = new DateOnly(2026, 11, 20);

        var balance = RealBalanceCalculator.Calculate(account, until, [], [], [], []);

        Assert.Equal(until, balance.Until);
        Assert.Equal(M(-250.50m), balance.InitialBalance);
        Assert.Equal(Money.Zero, balance.Incomes);
        Assert.Equal(Money.Zero, balance.DirectExpenses);
        Assert.Equal(Money.Zero, balance.FixedExpenses);
        Assert.Equal(Money.Zero, balance.CardInvoices);
        Assert.Equal(M(-250.50m), balance.Balance);
    }

    [Fact]
    public void Calculate_AutoSettlesCreditCardInstallmentsOnDueDate_IgnoringFutureAndPreviouslyPaid()
    {
        var account = NewAccount(initialBalance: 1000.00m);

        // Expected in Oct, actually received in Nov -> cash regime uses ReceivedOn (Nov 5), NOT ExpectedDate!
        var income = Income.Create(Guid.NewGuid(), AccountId, new DateOnly(2026, 10, 28), M(500m), "Salário");
        income.MarkAsReceived(new DateOnly(2026, 11, 5), new DateOnly(2026, 12, 31));

        // Direct expense on Oct 10 (-100)
        var direct = DirectExpense.Create(
            Guid.NewGuid(), AccountId, new DateOnly(2026, 10, 10), M(100m), "Mercado", SystemCategories.FoodId);

        // Fixed expense paid on Oct 15 (-200)
        var fixedExp = FixedExpense.Create(
            Guid.NewGuid(), AccountId, YearMonth.Of(2026, 10), null, M(200m), "Internet", SystemCategories.ServicesId);
        fixedExp.MarkAsPaid(YearMonth.Of(2026, 10), new DateOnly(2026, 10, 15), new DateOnly(2026, 12, 31));

        // Credit card purchase on Oct 10 in 2x of R$ 300 (R$ 150 each):
        // - Inst 1: closes Nov 3, due Nov 12
        // - Inst 2: closes Dec 3, due Dec 12
        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, CardId, new DateOnly(2026, 10, 10), M(300m), 2, 1,
            Rule3_12, PurchaseCondition.Own, "Tênis", SystemCategories.ShoppingId);

        // On Oct 31: income NOT received yet (ReceivedOn = Nov 5), invoice NOT due yet
        var onOct31 = RealBalanceCalculator.Calculate(
            account, new DateOnly(2026, 10, 31), [income], [direct], [fixedExp], [purchase]);
        Assert.Equal(Money.Zero, onOct31.Incomes);
        Assert.Equal(M(100m), onOct31.DirectExpenses);
        Assert.Equal(M(200m), onOct31.FixedExpenses);
        Assert.Equal(Money.Zero, onOct31.CardInvoices);
        Assert.Equal(M(700m), onOct31.Balance);

        // Before Nov 12: income received on Nov 5 (+500), invoice still not due -> 1000 + 500 - 100 - 200 = 1200
        var beforeDue = RealBalanceCalculator.Calculate(
            account, new DateOnly(2026, 11, 11), [income], [direct], [fixedExp], [purchase]);
        Assert.Equal(M(500m), beforeDue.Incomes);
        Assert.Equal(M(100m), beforeDue.DirectExpenses);
        Assert.Equal(M(200m), beforeDue.FixedExpenses);
        Assert.Equal(Money.Zero, beforeDue.CardInvoices);
        Assert.Equal(M(1200m), beforeDue.Balance);

        // On Nov 12: 1st installment settles automatically -> 1200 - 150 = 1050
        var onFirstDue = RealBalanceCalculator.Calculate(
            account, new DateOnly(2026, 11, 12), [income], [direct], [fixedExp], [purchase]);
        Assert.Equal(M(150m), onFirstDue.CardInvoices);
        Assert.Equal(M(1050m), onFirstDue.Balance);

        // On Dec 12: 2nd installment also settles -> 1200 - 300 = 900
        var onSecondDue = RealBalanceCalculator.Calculate(
            account, new DateOnly(2026, 12, 12), [income], [direct], [fixedExp], [purchase]);
        Assert.Equal(M(300m), onSecondDue.CardInvoices);
        Assert.Equal(M(900m), onSecondDue.Balance);
    }

    [Fact]
    public void Calculate_RespectsAccountIsolation_InitialBalanceDateBoundaries_AndFixedExpenseHistory()
    {
        var account = NewAccount(initialBalance: 1000m);

        // Exact InitialBalanceDate (Oct 1) counts; day before (Sep 30) is ignored; after until (Nov 11) is ignored
        var today = new DateOnly(2026, 11, 20);
        var incomeBefore = Income.Create(Guid.NewGuid(), AccountId, new DateOnly(2026, 9, 30), M(111m), "Antes");
        incomeBefore.MarkAsReceived(new DateOnly(2026, 9, 30), today);
        var incomeOnInitial = Income.Create(Guid.NewGuid(), AccountId, new DateOnly(2026, 10, 1), M(400m), "Dia inicial");
        incomeOnInitial.MarkAsReceived(new DateOnly(2026, 10, 1), today);
        var incomeExpectedOnly = Income.Create(Guid.NewGuid(), AccountId, new DateOnly(2026, 10, 5), M(999m), "Só prevista");
        var incomeAfterUntil = Income.Create(Guid.NewGuid(), AccountId, new DateOnly(2026, 11, 15), M(888m), "Futura");
        incomeAfterUntil.MarkAsReceived(new DateOnly(2026, 11, 15), today);
        var incomeOtherAccount = Income.Create(Guid.NewGuid(), OtherAccountId, new DateOnly(2026, 10, 5), M(777m), "Outra conta");
        incomeOtherAccount.MarkAsReceived(new DateOnly(2026, 10, 5), today);

        var directBefore = DirectExpense.Create(Guid.NewGuid(), AccountId, new DateOnly(2026, 9, 30), M(50m), "Antes", SystemCategories.FoodId);
        var directOnInitial = DirectExpense.Create(Guid.NewGuid(), AccountId, new DateOnly(2026, 10, 1), M(60m), "Inicial", SystemCategories.FoodId);
        var directOnUntil = DirectExpense.Create(Guid.NewGuid(), AccountId, new DateOnly(2026, 11, 10), M(40m), "Limite", SystemCategories.FoodId);
        var directAfterUntil = DirectExpense.Create(Guid.NewGuid(), AccountId, new DateOnly(2026, 11, 11), M(99m), "Depois", SystemCategories.FoodId);
        var directOtherAccount = DirectExpense.Create(Guid.NewGuid(), OtherAccountId, new DateOnly(2026, 10, 5), M(500m), "Outra conta", SystemCategories.FoodId);

        // Fixed expense changes amount from 100 (Oct) to 150 (Nov); both months paid -> 100 + 150 = 250
        var fixedWithHistory = FixedExpense.Create(Guid.NewGuid(), AccountId, YearMonth.Of(2026, 10), null, M(100m), "Academia", SystemCategories.HealthId);
        fixedWithHistory.ChangeAmount(M(150m), new DateOnly(2026, 11, 1));
        fixedWithHistory.MarkAsPaid(YearMonth.Of(2026, 10), new DateOnly(2026, 10, 5), today);
        fixedWithHistory.MarkAsPaid(YearMonth.Of(2026, 11), new DateOnly(2026, 11, 10), today);

        var fixedUnpaid = FixedExpense.Create(Guid.NewGuid(), AccountId, YearMonth.Of(2026, 10), null, M(300m), "Não paga", SystemCategories.ServicesId);
        var fixedOtherAccount = FixedExpense.Create(Guid.NewGuid(), OtherAccountId, YearMonth.Of(2026, 10), null, M(600m), "Outra conta", SystemCategories.ServicesId);
        fixedOtherAccount.MarkAsPaid(YearMonth.Of(2026, 10), new DateOnly(2026, 10, 5), today);

        var balance = RealBalanceCalculator.Calculate(
            account,
            new DateOnly(2026, 11, 10),
            [incomeBefore, incomeOnInitial, incomeExpectedOnly, incomeAfterUntil, incomeOtherAccount],
            [directBefore, directOnInitial, directOnUntil, directAfterUntil, directOtherAccount],
            [fixedWithHistory, fixedUnpaid, fixedOtherAccount],
            []);

        Assert.Equal(M(400m), balance.Incomes);
        Assert.Equal(M(100m), balance.DirectExpenses);
        Assert.Equal(M(250m), balance.FixedExpenses);
        Assert.Equal(Money.Zero, balance.CardInvoices);
        Assert.Equal(M(1050m), balance.Balance);
    }

    [Fact]
    public void Calculate_HandlesDueDayBeforeClosingDay_ExactInitialDateDueDate_AndArchivedCards()
    {
        var account = NewAccount(initialBalance: 1000m);

        // Rule (25, 5): purchase on Oct 10 closes Oct 25, due Nov 5 (next month!).
        var dueNextMonth = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, CardId, new DateOnly(2026, 10, 10), M(120m), 1, 1,
            Rule25_5, PurchaseCondition.Others, "Cartão ativo", SystemCategories.ShoppingId);

        // Archived card purchase with DueDate == InitialBalanceDate (Oct 1) using rule (20, 1):
        // Sep 10 closes Sep 20, due Oct 1 -> exact lower bound counts!
        var archivedCardPurchase = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, ArchivedCardId, new DateOnly(2026, 9, 10), M(80m), 1, 1,
            CardBillingRule.Of(20, 1), PurchaseCondition.Custom("Mãe"), "Cartão arquivado", SystemCategories.ShoppingId);

        // On Oct 31: archived card (due Oct 1) settled; dueNextMonth (closed Oct 25, due Nov 5) NOT settled yet!
        var onOct31 = RealBalanceCalculator.Calculate(
            account, new DateOnly(2026, 10, 31), [], [], [], [dueNextMonth, archivedCardPurchase]);
        Assert.Equal(M(80m), onOct31.CardInvoices);
        Assert.Equal(M(920m), onOct31.Balance);

        // On Nov 5: both settled (80 + 120 = 200)
        var onNov5 = RealBalanceCalculator.Calculate(
            account, new DateOnly(2026, 11, 5), [], [], [], [dueNextMonth, archivedCardPurchase]);
        Assert.Equal(M(200m), onNov5.CardInvoices);
        Assert.Equal(M(800m), onNov5.Balance);
    }

    [Fact]
    public void Calculate_IgnoresInstallmentsBeforeInitialBalanceDate_PreviouslyPaid_AndOtherAccounts()
    {
        var account = NewAccount(initialBalance: 1000m);

        // Due Sep 12 (before InitialDate Oct 1) -> ignored
        var beforeInitial = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, CardId, new DateOnly(2026, 8, 10), M(200m), 1, 1,
            Rule3_12, PurchaseCondition.Own, "Antiga", SystemCategories.ShoppingId);

        // Previously paid installment 1 due Oct 12 -> ignored; installment 2 due Nov 12 -> counts
        var inProgress = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, CardId, new DateOnly(2026, 9, 10), M(200m), 2, 2,
            Rule3_12, PurchaseCondition.Own, "Em andamento", SystemCategories.ShoppingId);

        // Other account -> ignored
        var otherAccount = CreditCardPurchase.Create(
            Guid.NewGuid(), OtherAccountId, CardId, new DateOnly(2026, 10, 10), M(999m), 1, 1,
            Rule3_12, PurchaseCondition.Own, "Outra conta", SystemCategories.ShoppingId);

        var balance = RealBalanceCalculator.Calculate(
            account, new DateOnly(2026, 12, 31), [], [], [], [beforeInitial, inProgress, otherAccount]);

        Assert.Equal(M(100m), balance.CardInvoices);
        Assert.Equal(M(900m), balance.Balance);
    }

    [Fact]
    public void PurchaseBeforeInitialBalanceDate_WithOpenDueDate_IntegratesConsistentlyAcrossBalanceLimitInvoiceAndSurplus()
    {
        // Account created 23/11 with R$ 2000; purchase on 08/11 of R$ 300 on card (25, 5) -> closes 25/11, due 05/12.
        var initialDate = new DateOnly(2026, 11, 23);
        var account = Account.Create(AccountId, OwnerUserId, "Conta", M(2000m), initialDate, isInitialBalanceConfirmed: true);
        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, CardId, new DateOnly(2026, 11, 8), M(300m), 1, 1,
            Rule25_5, PurchaseCondition.Own, "Compra pré-saldo", SystemCategories.ShoppingId);

        purchase.EnsureFirstActiveDueDateIsOpen(account, initialDate);

        // On 23/11: RealBalance untouched (2000), limit used = 300, Nov closing invoice = 300
        var realOn23Nov = RealBalanceCalculator.Calculate(account, initialDate, [], [], [], [purchase]);
        Assert.Equal(Money.Zero, realOn23Nov.CardInvoices);
        Assert.Equal(M(2000m), realOn23Nov.Balance);
        Assert.Equal(M(300m), CreditCardLimitCalculator.CalculateUsedLimit(CardId, initialDate, [purchase]));
        Assert.Equal(M(300m), InvoiceCalculator.Calculate(AccountId, YearMonth.Of(2026, 11), [purchase]));

        // Dec surplus sees 300 due on 05/12 and projects 1700 closing surplus
        var surplusDec = MonthlySurplusCalculator.CalculateSeries(
            account, YearMonth.Of(2026, 12), initialDate, [], [], [], [purchase], pastMonths: 0, futureMonths: 1);
        Assert.Equal(M(300m), surplusDec.SelectedMonth.CardInvoiceDue);
        Assert.Equal(M(1700m), surplusDec.SelectedMonth.ProjectedSurplus);

        // On 05/12: RealBalance auto-settles the 300 installment -> 1700, limit released -> 0
        var dueDate = new DateOnly(2026, 12, 5);
        var realOnDue = RealBalanceCalculator.Calculate(account, dueDate, [], [], [], [purchase]);
        Assert.Equal(M(300m), realOnDue.CardInvoices);
        Assert.Equal(M(1700m), realOnDue.Balance);
        Assert.Equal(Money.Zero, CreditCardLimitCalculator.CalculateUsedLimit(CardId, dueDate, [purchase]));
    }
}
