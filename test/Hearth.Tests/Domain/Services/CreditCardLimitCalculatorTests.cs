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
    private static readonly CardBillingRule Rule25_5 = CardBillingRule.Of(25, 5);

    [Fact]
    public void GuardClauses_RejectEmptyIdAndNullArguments()
    {
        var card = CreditCard.Create(CardId, AccountId, Rule3_12, "Nubank", Money.FromDecimal(500m));
        var today = new DateOnly(2026, 11, 20);

        Assert.Throws<ArgumentException>(() => CreditCardLimitCalculator.CalculateUsedLimit(Guid.Empty, today, []));
        Assert.Throws<ArgumentNullException>(() => CreditCardLimitCalculator.CalculateUsedLimit(CardId, today, null!));
        Assert.Throws<ArgumentNullException>(() => CreditCardLimitCalculator.CalculateAvailableLimit(null!, today, []));
        Assert.Throws<ArgumentNullException>(() => CreditCardLimitCalculator.CalculateAvailableLimit(card, today, null!));
    }

    [Fact]
    public void SymmetryWithRealBalance_EveryInstallmentIsEitherInLimitOrInPaidInvoices_WithConcreteValues()
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
            SystemPurchaseConditions.OwnId,
            "Compra",
            SystemCategories.ShoppingId);

        (DateOnly Day, decimal ExpectedUsed, decimal ExpectedPaid)[] checkpoints =
        [
            (new DateOnly(2026, 11, 11), 300m, 0m),
            (new DateOnly(2026, 11, 12), 200m, 100m),
            (new DateOnly(2026, 12, 11), 200m, 100m),
            (new DateOnly(2026, 12, 12), 100m, 200m),
            (new DateOnly(2027, 1, 12), 0m, 300m),
        ];

        foreach (var (day, expectedUsed, expectedPaid) in checkpoints)
        {
            var used = CreditCardLimitCalculator.CalculateUsedLimit(CardId, day, [purchase]);
            var available = CreditCardLimitCalculator.CalculateAvailableLimit(card, day, [purchase]);
            var balance = RealBalanceCalculator.Calculate(account, day, [], [], [], [purchase]);

            Assert.Equal(Money.FromDecimal(expectedUsed), used);
            Assert.Equal(Money.FromDecimal(expectedPaid), balance.CardInvoices);
            Assert.Equal(Money.FromDecimal(300m), used + balance.CardInvoices);
            Assert.Equal(Money.FromDecimal(500m - expectedUsed), available);
        }
    }

    [Fact]
    public void CalculateUsedLimit_WithDueDayBeforeClosingDayAcrossYearRollover_ReleasesLimitOnDueDate()
    {
        // Rule (25, 5): purchase on 2026-12-10 closes 2026-12-25, due 2027-01-05.
        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, CardId, new DateOnly(2026, 12, 10), Money.FromDecimal(250m),
            1, 1, Rule25_5, Guid.NewGuid(), "Presente", SystemCategories.ShoppingId);

        Assert.Equal(Money.FromDecimal(250m), CreditCardLimitCalculator.CalculateUsedLimit(CardId, new DateOnly(2026, 12, 26), [purchase]));
        Assert.Equal(Money.FromDecimal(250m), CreditCardLimitCalculator.CalculateUsedLimit(CardId, new DateOnly(2027, 1, 4), [purchase]));
        Assert.Equal(Money.Zero, CreditCardLimitCalculator.CalculateUsedLimit(CardId, new DateOnly(2027, 1, 5), [purchase]));
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
            SystemPurchaseConditions.OwnId,
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
            SystemPurchaseConditions.OwnId,
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
    public void CalculateAvailableLimit_ZeroUsedAndExactLimitBoundaries()
    {
        var card = CreditCard.Create(CardId, AccountId, Rule3_12, "Nubank", Money.FromDecimal(200m));
        var exactPurchase = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, CardId, new DateOnly(2026, 11, 10), Money.FromDecimal(200m),
            1, 1, Rule3_12, SystemPurchaseConditions.OwnId, "Exato", SystemCategories.ShoppingId);

        Assert.Equal(Money.FromDecimal(200m), CreditCardLimitCalculator.CalculateAvailableLimit(card, new DateOnly(2026, 11, 15), []));
        Assert.Equal(Money.Zero, CreditCardLimitCalculator.CalculateAvailableLimit(card, new DateOnly(2026, 11, 15), [exactPurchase]));
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
            SystemPurchaseConditions.OwnId,
            "Acima",
            SystemCategories.ShoppingId);

        var available = CreditCardLimitCalculator.CalculateAvailableLimit(
            card, new DateOnly(2026, 11, 15), [purchase]);

        Assert.Equal(Money.FromDecimal(-50m), available);
    }

    [Fact]
    public void CalculateUsedAndAvailableLimit_IgnoreInstallmentsDueBeforeInitialBalanceDate()
    {
        // Account initialBalanceDate = 2026-11-22.
        // Purchase on 2026-10-10 in 2x of R$ 200 (rule 3/12):
        // - Inst 1 due 2026-11-12 (< initialBalanceDate) -> must NEVER occupy limit, even if queried on 2026-11-01!
        // - Inst 2 due 2026-12-12 (>= initialBalanceDate) -> occupies R$ 100.
        var card = CreditCard.Create(CardId, AccountId, Rule3_12, "Nubank", Money.FromDecimal(1000m));
        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(),
            AccountId,
            CardId,
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(200m),
            2,
            1,
            Rule3_12,
            SystemPurchaseConditions.OwnId,
            "Histórica + futura",
            SystemCategories.ShoppingId);

        var initialBalanceDate = new DateOnly(2026, 11, 22);
        var queryBeforeDue = new DateOnly(2026, 11, 1);

        Assert.Equal(
            Money.FromDecimal(100m),
            CreditCardLimitCalculator.CalculateUsedLimit(CardId, queryBeforeDue, [purchase], initialBalanceDate));
        Assert.Equal(
            Money.FromDecimal(900m),
            CreditCardLimitCalculator.CalculateAvailableLimit(card, queryBeforeDue, [purchase], initialBalanceDate));
    }
}
