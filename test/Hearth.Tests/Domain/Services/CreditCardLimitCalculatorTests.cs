using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.Services;
using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.Services;

public sealed class CreditCardLimitCalculatorTests
{
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid OwnerUserId = Guid.NewGuid();
    private static readonly Guid CardId = Guid.NewGuid();
    private static readonly CardBillingRule Rule3_12 = CardBillingRule.Of(3, 12);

    [Fact]
    public void SymmetryWithRealBalance_EveryInstallmentIsEitherInLimitOrInPaidInvoices_NeverBothOrNeither()
    {
        // Purchase on 2026-10-10 in 3x of R$ 300 (R$ 100 each):
        // - Installment 1: closes 2026-11-03, due 2026-11-12
        // - Installment 2: closes 2026-12-03, due 2026-12-12
        // - Installment 3: closes 2027-01-03, due 2027-01-12
        var account = Account.Create(AccountId, OwnerUserId, "Conta", Money.FromDecimal(1000m), new DateOnly(2026, 1, 1));
        var card = CreditCard.Create(CardId, AccountId, Rule3_12, "Nubank", Money.FromDecimal(500m));
        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(),
            AccountId,
            CardId,
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(300m),
            3,
            1,
            Rule3_12,
            PurchaseCondition.Own,
            "Compra",
            SystemCategories.ShoppingId);

        DateOnly[] checkDates =
        [
            new(2026, 11, 11), // day before 1st due date -> all 300 in limit, 0 paid
            new(2026, 11, 12), // exact 1st due date -> 200 in limit, 100 paid
            new(2026, 12, 11), // day before 2nd due date -> 200 in limit, 100 paid
            new(2026, 12, 12), // exact 2nd due date -> 100 in limit, 200 paid
            new(2027, 1, 12),  // exact 3rd due date -> 0 in limit, 300 paid
        ];

        foreach (var day in checkDates)
        {
            var used = CreditCardLimitCalculator.CalculateUsedLimit(CardId, day, [purchase]);
            var available = CreditCardLimitCalculator.CalculateAvailableLimit(card, day, [purchase]);
            var balance = RealBalanceCalculator.Calculate(account, day, [], [], [], [purchase]);

            Assert.Equal(Money.FromDecimal(300m), used + balance.CardInvoices);
            Assert.Equal(Money.FromDecimal(500m) - used, available);
        }
    }

    [Fact]
    public void CalculateUsedLimit_IgnoresPreviouslyPaidInstallmentsAndOtherCards()
    {
        var otherCardId = Guid.NewGuid();
        var inProgress = CreditCardPurchase.Create(
            Guid.NewGuid(),
            AccountId,
            CardId,
            new DateOnly(2026, 9, 10), // closes Oct, Nov, Dec; due Oct 12, Nov 12, Dec 12
            Money.FromDecimal(300m),
            3,
            3, // only installment 3 (due Dec 12) counts
            Rule3_12,
            PurchaseCondition.Own,
            "Antiga",
            SystemCategories.ShoppingId);

        var otherCardPurchase = CreditCardPurchase.Create(
            Guid.NewGuid(),
            AccountId,
            otherCardId,
            new DateOnly(2026, 11, 10),
            Money.FromDecimal(500m),
            1,
            1,
            Rule3_12,
            PurchaseCondition.Own,
            "Outro Cartão",
            SystemCategories.ShoppingId);

        var used = CreditCardLimitCalculator.CalculateUsedLimit(
            CardId, new DateOnly(2026, 10, 1), [inProgress, otherCardPurchase]);

        Assert.Equal(Money.FromDecimal(100m), used);
    }

    [Fact]
    public void CalculateAvailableLimit_WhenLimitIsNull_ReturnsNull()
    {
        var card = CreditCard.Create(CardId, AccountId, Rule3_12, "Sem limite", limit: null);

        Assert.Null(CreditCardLimitCalculator.CalculateAvailableLimit(card, new DateOnly(2026, 11, 1), []));
    }

    [Fact]
    public void CalculateAvailableLimit_CanBeNegativeWhenSpendingExceedsLimit()
    {
        var card = CreditCard.Create(CardId, AccountId, Rule3_12, "Nubank", Money.FromDecimal(100m));
        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(),
            AccountId,
            CardId,
            new DateOnly(2026, 11, 10),
            Money.FromDecimal(150m),
            1,
            1,
            Rule3_12,
            PurchaseCondition.Own,
            "Acima",
            SystemCategories.ShoppingId);

        var available = CreditCardLimitCalculator.CalculateAvailableLimit(
            card, new DateOnly(2026, 11, 15), [purchase]);

        Assert.Equal(Money.FromDecimal(-50m), available);
    }

    [Fact]
    public void GetInvoiceStatus_TransitionsAtClosingDateAndDueDate()
    {
        var cycle = BillingCycle.Closing(YearMonth.Of(2026, 11), Rule3_12); // closes Nov 3, due Nov 12

        Assert.Equal(InvoiceStatus.Open, CreditCardLimitCalculator.GetInvoiceStatus(cycle, new DateOnly(2026, 11, 2)));
        Assert.Equal(InvoiceStatus.Closed, CreditCardLimitCalculator.GetInvoiceStatus(cycle, new DateOnly(2026, 11, 3)));
        Assert.Equal(InvoiceStatus.Closed, CreditCardLimitCalculator.GetInvoiceStatus(cycle, new DateOnly(2026, 11, 11)));
        Assert.Equal(InvoiceStatus.Paid, CreditCardLimitCalculator.GetInvoiceStatus(cycle, new DateOnly(2026, 11, 12)));
        Assert.Equal(InvoiceStatus.Paid, CreditCardLimitCalculator.GetInvoiceStatus(cycle, new DateOnly(2026, 11, 30)));
    }
}
