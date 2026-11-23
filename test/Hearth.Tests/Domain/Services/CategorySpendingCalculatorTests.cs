using Hearth.Domain.Entities;
using Hearth.Domain.Services;
using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.Services;

public class CategorySpendingCalculatorTests
{
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid OtherAccountId = Guid.NewGuid();
    private static readonly Guid CardId = Guid.NewGuid();
    private static readonly CardBillingRule Rule3 = CardBillingRule.Of(3, 12);

    private static readonly Guid TransportCategoryId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid FoodCategoryId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid HealthCategoryId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid EducationCategoryId = Guid.Parse("00000000-0000-0000-0000-000000000004");
    private static readonly Guid EntertainmentCategoryId = Guid.Parse("00000000-0000-0000-0000-000000000005");
    private static readonly Guid ServicesCategoryId = Guid.Parse("00000000-0000-0000-0000-000000000006");
    private static readonly Guid ShoppingCategoryId = Guid.Parse("00000000-0000-0000-0000-000000000007");
    private static readonly Guid PersonalCategoryId = Guid.Parse("00000000-0000-0000-0000-000000000008");

    private static readonly Guid[] AllCategoryIds =
    [
        TransportCategoryId,
        FoodCategoryId,
        HealthCategoryId,
        EducationCategoryId,
        EntertainmentCategoryId,
        ServicesCategoryId,
        ShoppingCategoryId,
        PersonalCategoryId,
    ];

    private static Money M(decimal amount) => Money.FromDecimal(amount);

    private static YearMonth YM(int year, int month) => YearMonth.Of(year, month);

    private static DateOnly Day(int year, int month, int day) => new(year, month, day);

    private static DirectExpense NewDirect(
        DateOnly date,
        decimal amount,
        Guid? categoryId = null,
        Guid? accountId = null) =>
        DirectExpense.Create(
            Guid.NewGuid(), accountId ?? AccountId, date, M(amount), "Direto", categoryId ?? FoodCategoryId);

    private static FixedExpense NewFixed(
        YearMonth start,
        YearMonth? end,
        decimal amount,
        Guid? categoryId = null,
        Guid? accountId = null) =>
        FixedExpense.Create(
            Guid.NewGuid(), accountId ?? AccountId, start, end, M(amount), "Fixa", categoryId ?? ServicesCategoryId);

    private static CreditCardPurchase NewPurchase(
        DateOnly date,
        decimal amount,
        Guid? categoryId = null,
        PurchaseCondition? condition = null,
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
            condition ?? PurchaseCondition.Own,
            "Compra",
            categoryId ?? ShoppingCategoryId);

    private static CategorySpending Calc(
        YearMonth month,
        IEnumerable<DirectExpense>? direct = null,
        IEnumerable<FixedExpense>? fixedExpenses = null,
        IEnumerable<CreditCardPurchase>? purchases = null,
        bool includeDirect = true,
        bool includeFixed = true,
        bool includeCard = true,
        IReadOnlyCollection<PurchaseCondition>? includedConditions = null) =>
        CategorySpendingCalculator.Calculate(
            AccountId,
            month,
            direct ?? Array.Empty<DirectExpense>(),
            fixedExpenses ?? Array.Empty<FixedExpense>(),
            purchases ?? Array.Empty<CreditCardPurchase>(),
            includeDirect,
            includeFixed,
            includeCard,
            includedConditions);

    // ---------- Empty ----------

    [Fact]
    public void Calculate_WithNoData_ReturnsEmptyAndZeroTotals_AndKeepsTheMonth()
    {
        var result = Calc(YM(2026, 11));

        Assert.Equal(YM(2026, 11), result.Month);
        Assert.Empty(result.Categories);
        Assert.Equal(Money.Zero, result.DirectExpenses);
        Assert.Equal(Money.Zero, result.FixedExpenses);
        Assert.Equal(Money.Zero, result.CardInstallments);
        Assert.Equal(Money.Zero, result.Total);
    }

    // ---------- Competence rules ----------

    [Fact]
    public void Calculate_GroupsDirectFixedAndCardInstallmentsInTheSameCategory()
    {
        var direct = NewDirect(Day(2026, 11, 10), 120.50m, FoodCategoryId);
        var fixedExpense = NewFixed(YM(2026, 1), null, 300.00m, FoodCategoryId);
        var purchase = NewPurchase(Day(2026, 10, 10), 100.00m, FoodCategoryId, count: 3);

        var result = Calc(
            YM(2026, 11),
            direct: new[] { direct },
            fixedExpenses: new[] { fixedExpense },
            purchases: new[] { purchase });

        var item = Assert.Single(result.Categories);
        Assert.Equal(FoodCategoryId, item.CategoryId);
        Assert.Equal(M(120.50m), item.DirectExpenses);
        Assert.Equal(M(300.00m), item.FixedExpenses);
        Assert.Equal(M(33.33m), item.CardInstallments);
        Assert.Equal(M(453.83m), item.Total);

        Assert.Equal(M(120.50m), result.DirectExpenses);
        Assert.Equal(M(300.00m), result.FixedExpenses);
        Assert.Equal(M(33.33m), result.CardInstallments);
        Assert.Equal(M(453.83m), result.Total);
    }

    [Fact]
    public void Calculate_DirectExpenses_CountOnlyInTheirMonth()
    {
        var before = NewDirect(Day(2026, 10, 31), 50.00m, TransportCategoryId);
        var first = NewDirect(Day(2026, 11, 1), 10.00m, TransportCategoryId);
        var last = NewDirect(Day(2026, 11, 30), 25.00m, TransportCategoryId);
        var after = NewDirect(Day(2026, 12, 1), 80.00m, TransportCategoryId);

        var result = Calc(YM(2026, 11), direct: new[] { before, first, last, after });

        var item = Assert.Single(result.Categories);
        Assert.Equal(M(35.00m), item.DirectExpenses);
        Assert.Equal(M(35.00m), result.Total);
    }

    [Fact]
    public void Calculate_FixedExpenses_UseAmountOfMonth_AndCountWhetherPaidOrNot()
    {
        var rent = NewFixed(YM(2026, 1), null, 1000.00m, ServicesCategoryId);
        rent.ChangeAmount(M(1200.00m), Day(2026, 7, 10));
        rent.MarkAsPaid(YM(2026, 7), Day(2026, 8, 2));

        var june = Calc(YM(2026, 6), fixedExpenses: new[] { rent });
        var july = Calc(YM(2026, 7), fixedExpenses: new[] { rent });

        Assert.Equal(M(1000.00m), june.FixedExpenses);
        Assert.Equal(M(1200.00m), july.FixedExpenses);
    }

    [Fact]
    public void Calculate_CardPurchases_CountByInstallmentClosingMonth_AndIgnorePreviouslyPaid()
    {
        var purchase = NewPurchase(Day(2026, 7, 10), 600.00m, EducationCategoryId, count: 6, initial: 4);

        Assert.Empty(Calc(YM(2026, 10), purchases: new[] { purchase }).Categories);

        var november = Calc(YM(2026, 11), purchases: new[] { purchase });
        var item = Assert.Single(november.Categories);
        Assert.Equal(EducationCategoryId, item.CategoryId);
        Assert.Equal(M(100.00m), item.CardInstallments);
    }

    // ---------- Ordering and zero omission ----------

    [Fact]
    public void Calculate_OmitsZeroCategories_AndOrdersByTotalDescendingThenCategoryIdAscending()
    {
        var food = NewDirect(Day(2026, 11, 5), 200.00m, FoodCategoryId);
        var personal = NewDirect(Day(2026, 11, 6), 200.00m, PersonalCategoryId);
        var transport = NewDirect(Day(2026, 11, 7), 200.00m, TransportCategoryId);
        var health = NewDirect(Day(2026, 11, 8), 350.00m, HealthCategoryId);
        var shopping = NewDirect(Day(2026, 11, 9), 50.00m, ShoppingCategoryId);

        var result = Calc(YM(2026, 11), direct: new[] { shopping, personal, food, health, transport });

        Assert.Equal(5, result.Categories.Count);
        Assert.Equal(HealthCategoryId, result.Categories[0].CategoryId);
        Assert.Equal(TransportCategoryId, result.Categories[1].CategoryId);
        Assert.Equal(FoodCategoryId, result.Categories[2].CategoryId);
        Assert.Equal(PersonalCategoryId, result.Categories[3].CategoryId);
        Assert.Equal(ShoppingCategoryId, result.Categories[4].CategoryId);
    }

    // ---------- Accounts ----------

    [Fact]
    public void Calculate_IgnoresItemsOfOtherAccounts()
    {
        var direct = NewDirect(Day(2026, 11, 5), 100.00m, FoodCategoryId, OtherAccountId);
        var fixedExpense = NewFixed(YM(2026, 1), null, 500.00m, ServicesCategoryId, OtherAccountId);
        var purchase = NewPurchase(Day(2026, 10, 10), 300.00m, ShoppingCategoryId, accountId: OtherAccountId);

        var result = Calc(
            YM(2026, 11),
            direct: new[] { direct },
            fixedExpenses: new[] { fixedExpense },
            purchases: new[] { purchase });

        Assert.Empty(result.Categories);
        Assert.Equal(Money.Zero, result.Total);
    }

    // ---------- Orthogonal source filters ----------

    [Fact]
    public void Calculate_SourceFlagsFilterEachSourceIndependently()
    {
        var direct = NewDirect(Day(2026, 11, 5), 100.00m, FoodCategoryId);
        var fixedExpense = NewFixed(YM(2026, 1), null, 200.00m, ServicesCategoryId);
        var purchase = NewPurchase(Day(2026, 10, 10), 300.00m, ShoppingCategoryId);

        var onlyCard = Calc(
            YM(2026, 11),
            direct: new[] { direct },
            fixedExpenses: new[] { fixedExpense },
            purchases: new[] { purchase },
            includeDirect: false,
            includeFixed: false,
            includeCard: true);

        Assert.Equal(Money.Zero, onlyCard.DirectExpenses);
        Assert.Equal(Money.Zero, onlyCard.FixedExpenses);
        Assert.Equal(M(300.00m), onlyCard.CardInstallments);
        Assert.Equal(ShoppingCategoryId, Assert.Single(onlyCard.Categories).CategoryId);

        var onlyDirect = Calc(
            YM(2026, 11),
            direct: new[] { direct },
            fixedExpenses: new[] { fixedExpense },
            purchases: new[] { purchase },
            includeDirect: true,
            includeFixed: false,
            includeCard: false);

        Assert.Equal(M(100.00m), onlyDirect.DirectExpenses);
        Assert.Equal(Money.Zero, onlyDirect.FixedExpenses);
        Assert.Equal(Money.Zero, onlyDirect.CardInstallments);
        Assert.Equal(FoodCategoryId, Assert.Single(onlyDirect.Categories).CategoryId);

        var none = Calc(
            YM(2026, 11),
            direct: new[] { direct },
            fixedExpenses: new[] { fixedExpense },
            purchases: new[] { purchase },
            includeDirect: false,
            includeFixed: false,
            includeCard: false);

        Assert.Empty(none.Categories);
        Assert.Equal(Money.Zero, none.Total);
    }

    // ---------- PurchaseCondition filter ----------

    [Fact]
    public void Calculate_ConditionFilter_IncludesOnlySelectedConditionsOnCardPurchases()
    {
        var direct = NewDirect(Day(2026, 11, 5), 50.00m, FoodCategoryId);
        var fixedExpense = NewFixed(YM(2026, 1), null, 100.00m, ServicesCategoryId);
        var own = NewPurchase(Day(2026, 10, 10), 200.00m, FoodCategoryId, PurchaseCondition.Own);
        var others = NewPurchase(Day(2026, 10, 11), 400.00m, FoodCategoryId, PurchaseCondition.Others);
        var trip = NewPurchase(Day(2026, 10, 12), 150.00m, EntertainmentCategoryId, PurchaseCondition.Custom("Viagem"));
        var mom = NewPurchase(Day(2026, 10, 13), 80.00m, HealthCategoryId, PurchaseCondition.Custom("Mae"));

        var result = Calc(
            YM(2026, 11),
            direct: new[] { direct },
            fixedExpenses: new[] { fixedExpense },
            purchases: new[] { own, others, trip, mom },
            includedConditions: new[] { PurchaseCondition.Own, PurchaseCondition.Custom("Viagem") });

        Assert.Equal(M(50.00m), result.DirectExpenses);
        Assert.Equal(M(100.00m), result.FixedExpenses);
        Assert.Equal(M(350.00m), result.CardInstallments);
        Assert.Equal(M(500.00m), result.Total);
        Assert.Equal(3, result.Categories.Count);
        Assert.DoesNotContain(result.Categories, item => item.CategoryId == HealthCategoryId);
    }

    [Fact]
    public void Calculate_ConditionFilter_UsesExactCustomNameMatch()
    {
        var upper = NewPurchase(Day(2026, 10, 10), 100.00m, EntertainmentCategoryId, PurchaseCondition.Custom("Viagem"));
        var lower = NewPurchase(Day(2026, 10, 11), 250.00m, EntertainmentCategoryId, PurchaseCondition.Custom("viagem"));

        var result = Calc(
            YM(2026, 11),
            purchases: new[] { upper, lower },
            includedConditions: new[] { PurchaseCondition.Custom("Viagem") });

        Assert.Equal(M(100.00m), result.CardInstallments);
    }

    [Fact]
    public void Calculate_ConditionFilter_EmptyListExcludesAllCardPurchases_WithoutAffectingDirectAndFixed()
    {
        var direct = NewDirect(Day(2026, 11, 5), 50.00m, FoodCategoryId);
        var fixedExpense = NewFixed(YM(2026, 1), null, 100.00m, ServicesCategoryId);
        var own = NewPurchase(Day(2026, 10, 10), 200.00m, ShoppingCategoryId, PurchaseCondition.Own);

        var result = Calc(
            YM(2026, 11),
            direct: new[] { direct },
            fixedExpenses: new[] { fixedExpense },
            purchases: new[] { own },
            includedConditions: Array.Empty<PurchaseCondition>());

        Assert.Equal(M(50.00m), result.DirectExpenses);
        Assert.Equal(M(100.00m), result.FixedExpenses);
        Assert.Equal(Money.Zero, result.CardInstallments);
        Assert.Equal(M(150.00m), result.Total);
    }

    [Fact]
    public void Calculate_OnlyOwnCardInstallments_ExcludesDirectFixedAndNonOwnCard()
    {
        var direct = NewDirect(Day(2026, 11, 5), 50.00m, FoodCategoryId);
        var fixedExpense = NewFixed(YM(2026, 1), null, 100.00m, ServicesCategoryId);
        var own = NewPurchase(Day(2026, 10, 10), 200.00m, FoodCategoryId, PurchaseCondition.Own);
        var others = NewPurchase(Day(2026, 10, 11), 400.00m, FoodCategoryId, PurchaseCondition.Others);

        var result = Calc(
            YM(2026, 11),
            direct: new[] { direct },
            fixedExpenses: new[] { fixedExpense },
            purchases: new[] { own, others },
            includeDirect: false,
            includeFixed: false,
            includeCard: true,
            includedConditions: new[] { PurchaseCondition.Own });

        var item = Assert.Single(result.Categories);
        Assert.Equal(FoodCategoryId, item.CategoryId);
        Assert.Equal(Money.Zero, item.DirectExpenses);
        Assert.Equal(Money.Zero, item.FixedExpenses);
        Assert.Equal(M(200.00m), item.CardInstallments);
        Assert.Equal(M(200.00m), result.Total);
    }

    // ---------- Arguments and stability ----------

    [Fact]
    public void Calculate_EmptyAccountId_Throws()
    {
        Assert.Throws<ArgumentException>(() => CategorySpendingCalculator.Calculate(
            Guid.Empty,
            YM(2026, 11),
            Array.Empty<DirectExpense>(),
            Array.Empty<FixedExpense>(),
            Array.Empty<CreditCardPurchase>()));
    }

    [Fact]
    public void Calculate_NullArguments_Throw()
    {
        var direct = Array.Empty<DirectExpense>();
        var fixedExpenses = Array.Empty<FixedExpense>();
        var purchases = Array.Empty<CreditCardPurchase>();

        Assert.Throws<ArgumentNullException>(() =>
            CategorySpendingCalculator.Calculate(AccountId, null!, direct, fixedExpenses, purchases));
        Assert.Throws<ArgumentNullException>(() =>
            CategorySpendingCalculator.Calculate(AccountId, YM(2026, 11), null!, fixedExpenses, purchases));
        Assert.Throws<ArgumentNullException>(() =>
            CategorySpendingCalculator.Calculate(AccountId, YM(2026, 11), direct, null!, purchases));
        Assert.Throws<ArgumentNullException>(() =>
            CategorySpendingCalculator.Calculate(AccountId, YM(2026, 11), direct, fixedExpenses, null!));
    }

    [Fact]
    public void Calculate_NullConditionEntry_Throws()
    {
        var conditions = new PurchaseCondition[] { PurchaseCondition.Own, null! };

        Assert.Throws<ArgumentException>(() => Calc(YM(2026, 11), includedConditions: conditions));
    }

    [Fact]
    public void Calculate_CalledTwice_ReturnsEqualResults()
    {
        var direct = NewDirect(Day(2026, 11, 5), 100.00m, FoodCategoryId);
        var purchase = NewPurchase(Day(2026, 10, 10), 300.00m, ShoppingCategoryId, count: 3);

        var first = Calc(YM(2026, 11), direct: new[] { direct }, purchases: new[] { purchase });
        var second = Calc(YM(2026, 11), direct: new[] { direct }, purchases: new[] { purchase });

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    // ---------- Main invariants ----------

    [Fact]
    public void UnfilteredSpending_ReconcilesWithProjectedResult_AndMatchesIndependentCalculation()
    {
        var random = new Random(20261120);
        var allConditions = new[]
        {
            PurchaseCondition.Own,
            PurchaseCondition.Others,
            PurchaseCondition.Custom("Viagem"),
            PurchaseCondition.Custom("Mae"),
        };

        var directs = new List<DirectExpense>();
        var fixeds = new List<FixedExpense>();
        var purchases = new List<CreditCardPurchase>();

        for (var i = 0; i < 120; i++)
        {
            var accountId = random.Next(4) == 0 ? OtherAccountId : AccountId;
            var categoryId = AllCategoryIds[random.Next(AllCategoryIds.Length)];

            directs.Add(NewDirect(
                Day(2026, 1, 1).AddDays(random.Next(0, 700)),
                random.Next(1, 50000) / 100m,
                categoryId,
                accountId));

            var start = YM(2026, 1).AddMonths(random.Next(0, 18));
            YearMonth? end = random.Next(2) == 0 ? null : start.AddMonths(random.Next(0, 20));
            var fixedExpense = NewFixed(
                start,
                end,
                random.Next(1, 100000) / 100m,
                AllCategoryIds[random.Next(AllCategoryIds.Length)],
                accountId);
            if (random.Next(2) == 0)
            {
                fixedExpense.MarkAsPaid(start, Day(2026, 1, 1).AddDays(random.Next(0, 700)));
            }

            fixeds.Add(fixedExpense);

            var count = random.Next(1, 13);
            var initial = random.Next(1, count + 1);
            var rule = CardBillingRule.Of(random.Next(1, 29), random.Next(1, 29));
            var condition = allConditions[random.Next(allConditions.Length)];
            purchases.Add(NewPurchase(
                Day(2026, 1, 1).AddDays(random.Next(0, 700)),
                random.Next(count, 200000) / 100m,
                AllCategoryIds[random.Next(AllCategoryIds.Length)],
                condition,
                count,
                initial,
                accountId,
                rule));
        }

        var reversedDirects = directs.AsEnumerable().Reverse().ToList();
        var reversedFixeds = fixeds.AsEnumerable().Reverse().ToList();
        var reversedPurchases = purchases.AsEnumerable().Reverse().ToList();
        var selectedConditions = new[] { PurchaseCondition.Own, PurchaseCondition.Custom("Viagem") };

        for (var month = YM(2026, 1); month <= YM(2028, 12); month = month.AddMonths(1))
        {
            var spending = Calc(month, directs, fixeds, purchases);
            var reversed = Calc(month, reversedDirects, reversedFixeds, reversedPurchases);
            var projected = ProjectedResultCalculator.Calculate(
                AccountId,
                month,
                Array.Empty<Income>(),
                directs,
                fixeds,
                purchases);
            var context = $"month={month.Year}-{month.Month:00}";

            Assert.True(spending == reversed, $"Spending depends on input order: {context}");
            Assert.True(spending.DirectExpenses == projected.DirectExpenses, $"Direct differs from ProjectedResult: {context}");
            Assert.True(spending.FixedExpenses == projected.FixedExpenses, $"Fixed differs from ProjectedResult: {context}");
            Assert.True(spending.CardInstallments == projected.CardInvoice, $"Card differs from ProjectedResult: {context}");
            Assert.True(
                spending.Total == projected.DirectExpenses + projected.FixedExpenses + projected.CardInvoice,
                $"Total does not reconcile with ProjectedResult: {context}");
            Assert.True(
                spending.Total == Money.Sum(spending.Categories.Select(c => c.Total)),
                $"Category totals do not sum to overall total: {context}");
            Assert.True(
                spending.Categories.All(c => c.Total > Money.Zero),
                $"Zero category was not omitted: {context}");

            var filtered = Calc(month, directs, fixeds, purchases, includedConditions: selectedConditions);
            Assert.True(filtered.DirectExpenses == spending.DirectExpenses, $"Condition filter altered direct: {context}");
            Assert.True(filtered.FixedExpenses == spending.FixedExpenses, $"Condition filter altered fixed: {context}");
            Assert.True(filtered.CardInstallments <= spending.CardInstallments, $"Filtered card exceeds total: {context}");
        }
    }
}
