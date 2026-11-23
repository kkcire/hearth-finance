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
    private static readonly CardBillingRule Rule3_12 = CardBillingRule.Of(3, 12);
    private static readonly DateOnly InitialDate = new(2026, 10, 1);

    private static Money M(decimal amount) => Money.FromDecimal(amount);

    private static Account NewAccount(decimal initialBalance = 1000.00m, Guid? id = null) =>
        Account.Create(id ?? AccountId, OwnerUserId, "Conta", M(initialBalance), InitialDate);

    [Fact]
    public void Calculate_AutoSettlesCreditCardInstallmentsOnDueDate_IgnoringFutureAndPreviouslyPaid()
    {
        var account = NewAccount(initialBalance: 1000.00m);

        // Income received on Oct 5 (+500)
        var income = Income.Create(Guid.NewGuid(), AccountId, new DateOnly(2026, 10, 5), M(500m), "Salário");
        income.MarkAsReceived(new DateOnly(2026, 10, 5));

        // Direct expense on Oct 10 (-100)
        var direct = DirectExpense.Create(
            Guid.NewGuid(), AccountId, new DateOnly(2026, 10, 10), M(100m), "Mercado", SystemCategories.FoodId);

        // Fixed expense paid on Oct 15 (-200)
        var fixedExp = FixedExpense.Create(
            Guid.NewGuid(), AccountId, YearMonth.Of(2026, 10), null, M(200m), "Internet", SystemCategories.ServicesId);
        fixedExp.MarkAsPaid(YearMonth.Of(2026, 10), new DateOnly(2026, 10, 15));

        // Credit card purchase on Oct 10 in 2x of R$ 300 (R$ 150 each):
        // - Inst 1: closes Nov 3, due Nov 12
        // - Inst 2: closes Dec 3, due Dec 12
        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, CardId, new DateOnly(2026, 10, 10), M(300m), 2, 1,
            Rule3_12, PurchaseCondition.Own, "Tênis", SystemCategories.ShoppingId);

        // Before Nov 12: invoice NOT settled yet -> 1000 + 500 - 100 - 200 = 1200
        var beforeDue = RealBalanceCalculator.Calculate(
            account, new DateOnly(2026, 11, 11), [income], [direct], [fixedExp], [purchase]);
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
}
