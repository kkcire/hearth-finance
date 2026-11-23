using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.Services;
using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.Services;

public class BudgetStatusCalculatorTests
{
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid OtherAccountId = Guid.NewGuid();
    private static readonly Guid CardId = Guid.NewGuid();
    private static readonly CardBillingRule Rule3 = CardBillingRule.Of(3, 12);

    private static readonly Guid TransportId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid FoodId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid LeisureId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid UnbudgetedId = Guid.Parse("00000000-0000-0000-0000-000000000004");

    private static Money M(decimal amount) => Money.FromDecimal(amount);

    private static YearMonth YM(int year, int month) => YearMonth.Of(year, month);

    private static DateOnly Day(int year, int month, int day) => new(year, month, day);

    [Fact]
    public void Calculate_IncludesZeroSpendingBudgetedCategories_ExcludesUnbudgeted_AndComputesPercentages()
    {
        var transportBudget = CategoryBudget.Create(Guid.NewGuid(), AccountId, TransportId, YM(2026, 1), null, M(160.00m));
        var foodBudget = CategoryBudget.Create(Guid.NewGuid(), AccountId, FoodId, YM(2026, 1), null, M(500.00m));
        var leisureBudget = CategoryBudget.Create(Guid.NewGuid(), AccountId, LeisureId, YM(2026, 1), null, M(200.00m));
        var otherAccountBudget = CategoryBudget.Create(Guid.NewGuid(), OtherAccountId, TransportId, YM(2026, 1), null, M(999.00m));

        var directTransport = DirectExpense.Create(Guid.NewGuid(), AccountId, Day(2026, 11, 5), M(200.00m), "Uber", TransportId);
        var fixedFood = FixedExpense.Create(Guid.NewGuid(), AccountId, YM(2026, 1), null, M(250.00m), "Marmita", FoodId);
        var cardFood = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, CardId, Day(2026, 10, 10), M(100.00m), 1, 1, Rule3, PurchaseCondition.Own, "Mercado", FoodId);
        var unbudgetedExpense = DirectExpense.Create(Guid.NewGuid(), AccountId, Day(2026, 11, 10), M(900.00m), "Extra", UnbudgetedId);

        var status = BudgetStatusCalculator.Calculate(
            AccountId,
            YM(2026, 11),
            new[] { transportBudget, foodBudget, leisureBudget, otherAccountBudget },
            new[] { directTransport, unbudgetedExpense },
            new[] { fixedFood },
            new[] { cardFood });

        Assert.Equal(YM(2026, 11), status.Month);
        Assert.Equal(M(860.00m), status.TotalBudgeted);
        Assert.Equal(M(550.00m), status.TotalSpent);
        Assert.Equal(M(310.00m), status.TotalRemaining);

        Assert.Equal(3, status.Items.Count);

        // Ordered by UsagePercentage descending: Transport (125%) > Food (70%) > Leisure (0%).
        Assert.Equal(TransportId, status.Items[0].CategoryId);
        Assert.Equal(M(160.00m), status.Items[0].BudgetAmount);
        Assert.Equal(M(200.00m), status.Items[0].SpentAmount);
        Assert.Equal(M(-40.00m), status.Items[0].RemainingAmount);
        Assert.True(status.Items[0].IsExceeded);
        Assert.Equal(125.00m, status.Items[0].UsagePercentage);

        Assert.Equal(FoodId, status.Items[1].CategoryId);
        Assert.Equal(M(500.00m), status.Items[1].BudgetAmount);
        Assert.Equal(M(350.00m), status.Items[1].SpentAmount);
        Assert.Equal(M(150.00m), status.Items[1].RemainingAmount);
        Assert.False(status.Items[1].IsExceeded);
        Assert.Equal(70.00m, status.Items[1].UsagePercentage);

        Assert.Equal(LeisureId, status.Items[2].CategoryId);
        Assert.Equal(M(200.00m), status.Items[2].BudgetAmount);
        Assert.Equal(Money.Zero, status.Items[2].SpentAmount);
        Assert.Equal(M(200.00m), status.Items[2].RemainingAmount);
        Assert.False(status.Items[2].IsExceeded);
        Assert.Equal(0.00m, status.Items[2].UsagePercentage);
    }

    [Fact]
    public void Calculate_RespectsPurchaseConditionFilter()
    {
        var foodBudget = CategoryBudget.Create(Guid.NewGuid(), AccountId, FoodId, YM(2026, 1), null, M(300.00m));
        var own = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, CardId, Day(2026, 10, 10), M(120.00m), 1, 1, Rule3, PurchaseCondition.Own, "Meu", FoodId);
        var others = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, CardId, Day(2026, 10, 11), M(400.00m), 1, 1, Rule3, PurchaseCondition.Others, "Terceiro", FoodId);

        var status = BudgetStatusCalculator.Calculate(
            AccountId,
            YM(2026, 11),
            new[] { foodBudget },
            Array.Empty<DirectExpense>(),
            Array.Empty<FixedExpense>(),
            new[] { own, others },
            includedConditions: new[] { PurchaseCondition.Own });

        var item = Assert.Single(status.Items);
        Assert.Equal(M(120.00m), item.SpentAmount);
        Assert.Equal(40.00m, item.UsagePercentage);
        Assert.False(item.IsExceeded);
    }

    [Fact]
    public void Calculate_OverlappingBudgetsForSameCategoryInMonth_ThrowsDomainException()
    {
        var first = CategoryBudget.Create(Guid.NewGuid(), AccountId, FoodId, YM(2026, 1), null, M(300.00m));
        var second = CategoryBudget.Create(Guid.NewGuid(), AccountId, FoodId, YM(2026, 6), null, M(400.00m));

        Assert.Throws<DomainException>(() => BudgetStatusCalculator.Calculate(
            AccountId,
            YM(2026, 11),
            new[] { first, second },
            Array.Empty<DirectExpense>(),
            Array.Empty<FixedExpense>(),
            Array.Empty<CreditCardPurchase>()));
    }

    [Fact]
    public void Calculate_GuardClauses_RejectEmptyAndNullArguments()
    {
        var month = YM(2026, 11);

        Assert.Throws<ArgumentException>(() => BudgetStatusCalculator.Calculate(Guid.Empty, month, [], [], [], []));
        Assert.Throws<ArgumentNullException>(() => BudgetStatusCalculator.Calculate(AccountId, null!, [], [], [], []));
        Assert.Throws<ArgumentNullException>(() => BudgetStatusCalculator.Calculate(AccountId, month, null!, [], [], []));
        Assert.Throws<ArgumentNullException>(() => BudgetStatusCalculator.Calculate(AccountId, month, [], null!, [], []));
        Assert.Throws<ArgumentNullException>(() => BudgetStatusCalculator.Calculate(AccountId, month, [], [], null!, []));
        Assert.Throws<ArgumentNullException>(() => BudgetStatusCalculator.Calculate(AccountId, month, [], [], [], null!));
    }

    [Fact]
    public void Calculate_RespectsSourceFlags_Exact100PercentNotExceeded_AwayFromZeroRounding_AndTieBreakers()
    {
        // Exact 100% (Food: 200/200 -> IsExceeded == false)
        var foodBudget = CategoryBudget.Create(Guid.NewGuid(), AccountId, FoodId, YM(2026, 1), null, M(200m));
        // Midpoint rounding AwayFromZero: 1 / 800 = 0.125% -> 0.13m (ToEven would give 0.12m)
        var transportBudget = CategoryBudget.Create(Guid.NewGuid(), AccountId, TransportId, YM(2026, 1), null, M(800m));
        // Tie on UsagePercentage (100%) with higher SpentAmount (Leisure: 300/300 comes before Food: 200/200)
        var leisureBudget = CategoryBudget.Create(Guid.NewGuid(), AccountId, LeisureId, YM(2026, 1), null, M(300m));

        var directFood = DirectExpense.Create(Guid.NewGuid(), AccountId, Day(2026, 11, 5), M(200m), "Mercado", FoodId);
        var fixedTransport = FixedExpense.Create(Guid.NewGuid(), AccountId, YM(2026, 1), null, M(1m), "Taxa", TransportId);
        var cardLeisure = CreditCardPurchase.Create(
            Guid.NewGuid(), AccountId, CardId, Day(2026, 10, 10), M(300m), 1, 1, Rule3, PurchaseCondition.Own, "Show", LeisureId);

        var status = BudgetStatusCalculator.Calculate(
            AccountId,
            YM(2026, 11),
            [foodBudget, transportBudget, leisureBudget],
            [directFood],
            [fixedTransport],
            [cardLeisure]);

        // Order: Leisure (100%, 300) > Food (100%, 200) > Transport (0.13%, 1)
        Assert.Equal(LeisureId, status.Items[0].CategoryId);
        Assert.False(status.Items[0].IsExceeded);
        Assert.Equal(100.00m, status.Items[0].UsagePercentage);

        Assert.Equal(FoodId, status.Items[1].CategoryId);
        Assert.False(status.Items[1].IsExceeded);
        Assert.Equal(100.00m, status.Items[1].UsagePercentage);

        Assert.Equal(TransportId, status.Items[2].CategoryId);
        Assert.Equal(0.13m, status.Items[2].UsagePercentage);

        // Source flags disabled
        var onlyFixed = BudgetStatusCalculator.Calculate(
            AccountId,
            YM(2026, 11),
            [foodBudget, transportBudget, leisureBudget],
            [directFood],
            [fixedTransport],
            [cardLeisure],
            includeDirectExpenses: false,
            includeFixedExpenses: true,
            includeCardInstallments: false);

        Assert.Equal(M(1m), onlyFixed.TotalSpent);

        // Structural equality & GetHashCode on BudgetStatus
        var same = BudgetStatusCalculator.Calculate(
            AccountId, YM(2026, 11), [foodBudget, transportBudget, leisureBudget],
            [directFood], [fixedTransport], [cardLeisure]);
        Assert.Equal(status, same);
        Assert.Equal(status.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(status, onlyFixed);
    }
}
