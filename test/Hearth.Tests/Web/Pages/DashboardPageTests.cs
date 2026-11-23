using Bunit;
using Hearth.Application.CategoryBudgets.CreateCategoryBudget;
using Hearth.Application.CreditCardPurchases.RegisterCreditCardPurchase;
using Hearth.Application.DirectExpenses.RegisterDirectExpense;
using Hearth.Application.FixedExpenses.RegisterFixedExpense;
using Hearth.Application.Incomes.RegisterIncome;
using Hearth.Domain;
using Hearth.Domain.ValueObjects;
using Hearth.Web.Components.Pages;

namespace Hearth.Tests.Web.Pages;

public sealed class DashboardPageTests : WebTestBase
{
    [Fact]
    public async Task Dashboard_DisplaysRealBalanceAndProjectedResultSeparatelyAndNavigatesMonths()
    {
        var accountId = await SeedAccountAsync(initialBalance: 1000m);

        // Expected income (received = 500, expected only = 200) in Nov 2026
        await Executor.ExecuteAsync<RegisterIncomeHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterIncomeCommand(accountId, new DateOnly(2026, 11, 5), 500m, "Freela", new DateOnly(2026, 11, 5)), ct),
            showErrorSnackbar: false);
        await Executor.ExecuteAsync<RegisterIncomeHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterIncomeCommand(accountId, new DateOnly(2026, 11, 25), 200m, "Bônus", null), ct),
            showErrorSnackbar: false);

        // Direct expense in Nov 2026 (300)
        await Executor.ExecuteAsync<RegisterDirectExpenseHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterDirectExpenseCommand(accountId, new DateOnly(2026, 11, 10), 300m, "Mercado", SystemCategories.FoodId), ct),
            showErrorSnackbar: false);

        // Budgets: Food = 200 (exceeded by 300), Transport = 150 (zero spent, must still appear)
        await Executor.ExecuteAsync<CreateCategoryBudgetHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new CreateCategoryBudgetCommand(accountId, SystemCategories.FoodId, 2026, 11, null, null, 200m), ct),
            showErrorSnackbar: false);
        await Executor.ExecuteAsync<CreateCategoryBudgetHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new CreateCategoryBudgetCommand(accountId, SystemCategories.TransportId, 2026, 11, null, null, 150m), ct),
            showErrorSnackbar: false);

        var cut = Render<DashboardPage>();

        // Real Balance = 1000 + 500 (received) - 300 (direct) = 1200
        Assert.Contains("1.200,00", cut.Find("[data-testid='real-balance-value']").TextContent);

        // Projected Result (Nov 2026) = 700 (all incomes) - 300 (direct) = 400
        Assert.Contains("400,00", cut.Find("[data-testid='projected-result-value']").TextContent);

        // Exceeded budget shows badge, and zero-spent budget (Transporte) still appears
        Assert.NotEmpty(cut.FindAll("[data-testid='budget-exceeded-badge']"));
        Assert.Contains("Transporte", cut.Find("[data-testid='dashboard-budgets-card']").TextContent);

        // Navigating to Dec 2026 updates Projected Result (0) while keeping Real Balance (1200) untouched
        await cut.Find("[data-testid='month-next']").ClickAsync();
        Assert.Contains("Dezembro de 2026", cut.Find("[data-testid='month-label']").TextContent);
        Assert.Contains("0,00", cut.Find("[data-testid='projected-result-value']").TextContent);
        Assert.Contains("1.200,00", cut.Find("[data-testid='real-balance-value']").TextContent);
    }

    [Fact]
    public async Task Dashboard_OriginTogglesAndDynamicCustomConditionFilterSpending()
    {
        var accountId = await SeedAccountAsync();
        var cardId = await SeedCreditCardAsync(accountId, closingDay: 3, dueDay: 12);
        var categoryId = SystemCategories.ShoppingId;

        await Executor.ExecuteAsync<RegisterDirectExpenseHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterDirectExpenseCommand(accountId, new DateOnly(2026, 11, 10), 100m, "Direto", categoryId), ct),
            showErrorSnackbar: false);

        await Executor.ExecuteAsync<RegisterFixedExpenseHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterFixedExpenseCommand(accountId, 2026, 11, null, null, 200m, "Fixo", categoryId), ct),
            showErrorSnackbar: false);

        // Card purchase registered on Oct 25 -> cycle Nov 2026 (closing Nov 3, due Nov 12)
        Clock.Today = new DateOnly(2026, 10, 25);
        await Executor.ExecuteAsync<RegisterCreditCardPurchaseHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterCreditCardPurchaseCommand(
                accountId, cardId, new DateOnly(2026, 10, 20), 400m, 1, 1,
                PurchaseConditionKind.Custom, "Mãe", "Presente", categoryId), ct),
            showErrorSnackbar: false);
        Clock.Today = new DateOnly(2026, 11, 20);

        var cut = Render<DashboardPage>();

        // Total = 100 + 200 + 400 = 700
        Assert.Contains("700,00", cut.Find("[data-testid='spending-total']").TextContent);

        // Turn off Fixed -> 100 + 400 = 500
        await cut.Find("[data-testid='toggle-origin-fixed']").ClickAsync();
        Assert.Contains("500,00", cut.Find("[data-testid='spending-total']").TextContent);

        // Filter by dynamic Custom chip "Mãe" -> keeps direct (100) + card matching "Mãe" (400) = 500;
        // Filter by "Própria" -> card (400) excluded -> 100
        Assert.NotEmpty(cut.FindAll("[data-testid='dashboard-condition-custom-Mãe']"));
        await cut.Find("[data-testid='dashboard-condition-own']").ClickAsync();
        Assert.Contains("100,00", cut.Find("[data-testid='spending-total']").TextContent);
        Assert.Equal("true", cut.Find("[data-testid='dashboard-condition-own']").GetAttribute("aria-pressed"));
        Assert.Equal("false", cut.Find("[data-testid='toggle-origin-fixed']").GetAttribute("aria-pressed"));
    }

    [Fact]
    public async Task Dashboard_UsesAccessibleLandmarksAndShowsNoCardSetupStateWhenAccountHasNoCard()
    {
        await SeedAccountAsync(initialBalance: 1000m);

        var cut = Render<DashboardPage>();

        Assert.Equal("Painel do Mês", cut.Find("h1").TextContent.Trim());
        Assert.Equal("Mês anterior", cut.Find("[data-testid='month-prev']").GetAttribute("aria-label"));
        Assert.Equal("Próximo mês", cut.Find("[data-testid='month-next']").GetAttribute("aria-label"));
        Assert.NotEmpty(cut.FindAll("[data-testid='dashboard-no-card-state']"));
        Assert.Equal("/cartao", cut.Find("[data-testid='dashboard-configure-card-link']").GetAttribute("href"));
    }

    [Fact]
    public async Task Dashboard_WithActiveCardAndLimit_DisplaysCardLimitSummary()
    {
        var accountId = await SeedAccountAsync(initialBalance: 1000m);
        await SeedCreditCardAsync(accountId, closingDay: 3, dueDay: 12, name: "Cartão Black", limit: 5000m);

        var cut = Render<DashboardPage>();

        var limitBox = cut.Find("[data-testid='dashboard-card-limit']");
        Assert.Contains("5.000,00", limitBox.TextContent);
        Assert.Contains("Cartão Black", cut.Find("[data-testid='dashboard-invoice-card']").TextContent);
    }
}
