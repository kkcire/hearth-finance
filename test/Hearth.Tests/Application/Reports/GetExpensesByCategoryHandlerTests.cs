using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Application.Reports.GetExpensesByCategory;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Reports;

public class GetExpensesByCategoryHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly InMemoryDirectExpenseRepository _directExpenses = new();
    private readonly InMemoryFixedExpenseRepository _fixedExpenses = new();
    private readonly InMemoryCreditCardPurchaseRepository _purchases = new();
    private readonly GetExpensesByCategoryHandler _handler;

    public GetExpensesByCategoryHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new GetExpensesByCategoryHandler(
            _accountAccess,
            _categories,
            _directExpenses,
            _fixedExpenses,
            _purchases);
    }

    private async Task<Account> SeedAccountAsync(string name = "Conta")
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            name,
            Money.Zero,
            new DateOnly(2026, 1, 1),
            CardBillingRule.Of(3, 12));
        await _accounts.AddAsync(account);
        return account;
    }

    private async Task<Category> SeedCategoryAsync(Guid accountId, string name)
    {
        var category = Category.Create(Guid.NewGuid(), accountId, name);
        await _categories.AddAsync(category);
        return category;
    }

    [Fact]
    public async Task ExecuteAsync_WithDefaultFilters_ReturnsAllSourcesGroupedByCategoryWithNames()
    {
        var account = await SeedAccountAsync("Minha");
        var otherAccount = await SeedAccountAsync("Outra");

        var food = await SeedCategoryAsync(account.Id, "Alimentação");
        var services = await SeedCategoryAsync(account.Id, "Serviços");
        var shopping = await SeedCategoryAsync(account.Id, "Compras");
        var otherFood = await SeedCategoryAsync(otherAccount.Id, "Alimentação");

        var direct = DirectExpense.Create(
            Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 10), Money.FromDecimal(120.00m), "Mercado", food.Id);
        var otherDirect = DirectExpense.Create(
            Guid.NewGuid(), otherAccount.Id, new DateOnly(2026, 11, 10), Money.FromDecimal(900.00m), "Outro", otherFood.Id);

        var rent = FixedExpense.Create(
            Guid.NewGuid(), account.Id, YearMonth.Of(2026, 1), null, Money.FromDecimal(1000.00m), "Aluguel", services.Id);

        var ownPurchase = CreditCardPurchase.Create(
            Guid.NewGuid(),
            account.Id,
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(80.00m),
            1,
            1,
            account.BillingRule,
            PurchaseCondition.Own,
            "Restaurante",
            food.Id);

        var othersPurchase = CreditCardPurchase.Create(
            Guid.NewGuid(),
            account.Id,
            new DateOnly(2026, 10, 11),
            Money.FromDecimal(300.00m),
            1,
            1,
            account.BillingRule,
            PurchaseCondition.Others,
            "Compra de terceiro",
            shopping.Id);

        await _directExpenses.AddAsync(direct);
        await _directExpenses.AddAsync(otherDirect);
        await _fixedExpenses.AddAsync(rent);
        await _purchases.AddAsync(ownPurchase);
        await _purchases.AddAsync(othersPurchase);

        var dto = await _handler.ExecuteAsync(new GetExpensesByCategoryQuery(account.Id, 2026, 11));

        Assert.Equal(account.Id, dto.AccountId);
        Assert.Equal(2026, dto.Year);
        Assert.Equal(11, dto.Month);
        Assert.Equal(120.00m, dto.DirectExpenses);
        Assert.Equal(1000.00m, dto.FixedExpenses);
        Assert.Equal(380.00m, dto.CardInstallments);
        Assert.Equal(1500.00m, dto.Total);

        Assert.Equal(3, dto.Categories.Count);
        Assert.Equal(services.Id, dto.Categories[0].CategoryId);
        Assert.Equal("Serviços", dto.Categories[0].CategoryName);
        Assert.Equal(1000.00m, dto.Categories[0].Total);

        Assert.Equal(shopping.Id, dto.Categories[1].CategoryId);
        Assert.Equal("Compras", dto.Categories[1].CategoryName);
        Assert.Equal(300.00m, dto.Categories[1].CardInstallments);

        Assert.Equal(food.Id, dto.Categories[2].CategoryId);
        Assert.Equal("Alimentação", dto.Categories[2].CategoryName);
        Assert.Equal(120.00m, dto.Categories[2].DirectExpenses);
        Assert.Equal(80.00m, dto.Categories[2].CardInstallments);
        Assert.Equal(200.00m, dto.Categories[2].Total);
    }

    [Fact]
    public async Task ExecuteAsync_ArchivedCategoryStillAppearsInHistoricalReport()
    {
        var account = await SeedAccountAsync();
        var college = await SeedCategoryAsync(account.Id, "Faculdade");
        college.Archive();

        var direct = DirectExpense.Create(
            Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 5), Money.FromDecimal(150.00m), "Livro", college.Id);
        await _directExpenses.AddAsync(direct);

        var dto = await _handler.ExecuteAsync(new GetExpensesByCategoryQuery(account.Id, 2026, 11));

        var item = Assert.Single(dto.Categories);
        Assert.Equal(college.Id, item.CategoryId);
        Assert.Equal("Faculdade", item.CategoryName);
        Assert.Equal(150.00m, item.Total);
    }

    [Fact]
    public async Task ExecuteAsync_WithOrthogonalSourceAndConditionFilters_FiltersCardOnlyForOwnAndCustom()
    {
        var account = await SeedAccountAsync();
        var food = await SeedCategoryAsync(account.Id, "Alimentação");
        var services = await SeedCategoryAsync(account.Id, "Serviços");
        var entertainment = await SeedCategoryAsync(account.Id, "Entretenimento");
        var shopping = await SeedCategoryAsync(account.Id, "Compras");

        var direct = DirectExpense.Create(
            Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 5), Money.FromDecimal(50.00m), "Pix", food.Id);
        var fixedExpense = FixedExpense.Create(
            Guid.NewGuid(), account.Id, YearMonth.Of(2026, 1), null, Money.FromDecimal(100.00m), "Internet", services.Id);

        var own = CreditCardPurchase.Create(
            Guid.NewGuid(),
            account.Id,
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(200.00m),
            1,
            1,
            account.BillingRule,
            PurchaseCondition.Own,
            "Propria",
            food.Id);
        var trip = CreditCardPurchase.Create(
            Guid.NewGuid(),
            account.Id,
            new DateOnly(2026, 10, 11),
            Money.FromDecimal(150.00m),
            1,
            1,
            account.BillingRule,
            PurchaseCondition.Custom("Viagem"),
            "Hotel",
            entertainment.Id);
        var others = CreditCardPurchase.Create(
            Guid.NewGuid(),
            account.Id,
            new DateOnly(2026, 10, 12),
            Money.FromDecimal(400.00m),
            1,
            1,
            account.BillingRule,
            PurchaseCondition.Others,
            "Terceiro",
            shopping.Id);

        await _directExpenses.AddAsync(direct);
        await _fixedExpenses.AddAsync(fixedExpense);
        await _purchases.AddAsync(own);
        await _purchases.AddAsync(trip);
        await _purchases.AddAsync(others);

        var dto = await _handler.ExecuteAsync(new GetExpensesByCategoryQuery(
            account.Id,
            2026,
            11,
            IncludeDirectExpenses: false,
            IncludeFixedExpenses: false,
            IncludeCardInstallments: true,
            IncludedConditions: new[]
            {
                new PurchaseConditionFilter(PurchaseConditionKind.Own),
                new PurchaseConditionFilter(PurchaseConditionKind.Custom, "Viagem"),
            }));

        Assert.Equal(0.00m, dto.DirectExpenses);
        Assert.Equal(0.00m, dto.FixedExpenses);
        Assert.Equal(350.00m, dto.CardInstallments);
        Assert.Equal(350.00m, dto.Total);
        Assert.Equal(2, dto.Categories.Count);
        Assert.Equal(food.Id, dto.Categories[0].CategoryId);
        Assert.Equal(entertainment.Id, dto.Categories[1].CategoryId);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidConditionFilter_Throws()
    {
        var account = await SeedAccountAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.ExecuteAsync(
            new GetExpensesByCategoryQuery(
                account.Id,
                2026,
                11,
                IncludedConditions: new[]
                {
                    new PurchaseConditionFilter(PurchaseConditionKind.Own, "ShouldNotHaveName"),
                })));

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.ExecuteAsync(
            new GetExpensesByCategoryQuery(
                account.Id,
                2026,
                11,
                IncludedConditions: new[]
                {
                    new PurchaseConditionFilter(PurchaseConditionKind.Custom, "   "),
                })));

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.ExecuteAsync(
            new GetExpensesByCategoryQuery(
                account.Id,
                2026,
                11,
                IncludedConditions: new PurchaseConditionFilter[] { null! })));
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.ExecuteAsync(new GetExpensesByCategoryQuery(unknownId, 2026, 11)));

        Assert.Equal(nameof(Account), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidMonth_Throws()
    {
        var account = await SeedAccountAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _handler.ExecuteAsync(new GetExpensesByCategoryQuery(account.Id, 2026, 13)));
    }

    [Fact]
    public async Task ExecuteAsync_NullQuery_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.ExecuteAsync(null!));
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GetExpensesByCategoryHandler(null!, _categories, _directExpenses, _fixedExpenses, _purchases));
        Assert.Throws<ArgumentNullException>(() =>
            new GetExpensesByCategoryHandler(_accountAccess, null!, _directExpenses, _fixedExpenses, _purchases));
        Assert.Throws<ArgumentNullException>(() =>
            new GetExpensesByCategoryHandler(_accountAccess, _categories, null!, _fixedExpenses, _purchases));
        Assert.Throws<ArgumentNullException>(() =>
            new GetExpensesByCategoryHandler(_accountAccess, _categories, _directExpenses, null!, _purchases));
        Assert.Throws<ArgumentNullException>(() =>
            new GetExpensesByCategoryHandler(_accountAccess, _categories, _directExpenses, _fixedExpenses, null!));
    }
}
