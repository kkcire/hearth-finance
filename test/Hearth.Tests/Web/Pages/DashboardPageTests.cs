using Bunit;
using Hearth.Application.CategoryBudgets.CreateCategoryBudget;
using Hearth.Application.CreditCardPurchases.RegisterCreditCardPurchase;
using Hearth.Application.DirectExpenses.RegisterDirectExpense;
using Hearth.Application.FixedExpenses.RegisterFixedExpense;
using Hearth.Application.Incomes.RegisterIncome;
using Hearth.Application.PurchaseConditions;
using Hearth.Application.PurchaseConditions.CreatePurchaseCondition;
using Hearth.Domain;
using Hearth.Web.Components.Pages;
using Hearth.Web.Services;

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

        var momCondition = await Executor.ExecuteAsync<CreatePurchaseConditionHandler, PurchaseConditionDto>(
            (h, ct) => h.ExecuteAsync(new CreatePurchaseConditionCommand("Mãe"), ct),
            showErrorSnackbar: false);

        // Card purchase registered on Oct 25 -> cycle Nov 2026 (closing Nov 3, due Nov 12)
        Clock.Today = new DateOnly(2026, 10, 25);
        await Executor.ExecuteAsync<RegisterCreditCardPurchaseHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterCreditCardPurchaseCommand(
                accountId, cardId, new DateOnly(2026, 10, 20), 400m, 1, 1,
                momCondition.Value!.Id, "Presente", categoryId), ct),
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
        await cut.Find("[data-testid='dashboard-condition-custom-Mãe']").ClickAsync();
        Assert.Contains("500,00", cut.Find("[data-testid='spending-total']").TextContent);
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

    [Fact]
    public async Task Dashboard_QuickActionCardPurchase_UsesComprasRouteWithoutNovaQueryParam()
    {
        await SeedAccountAsync();

        var cut = Render<DashboardPage>();

        var quickAction = cut.Find("[data-testid='quick-action-card-purchase']");
        Assert.Equal("/cartao?aba=compras", quickAction.GetAttribute("href"));
    }

    [Fact]
    public async Task Dashboard_ExposesMonthlySurplusCascadeSeriesAndAverageVariation_WithFutureDirectExpensesFlag()
    {
        Clock.Today = new DateOnly(2026, 11, 15);
        var accountId = await SeedAccountAsync(initialBalance: 2000m, initialBalanceDate: new DateOnly(2026, 10, 1));
        var cardId = await SeedCreditCardAsync(accountId, closingDay: 25, dueDay: 5, name: "Nubank", limit: 5000m);

        // Oct received income (+1000) -> Oct historical closing = 3000
        await Executor.ExecuteAsync<RegisterIncomeHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterIncomeCommand(accountId, new DateOnly(2026, 10, 5), 1000m, "Freela Out", new DateOnly(2026, 10, 5)), ct),
            showErrorSnackbar: false);

        // Nov expected income (+1500), direct settled <= today (-200), direct future > today (-100), unpaid fixed (-500)
        await Executor.ExecuteAsync<RegisterIncomeHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterIncomeCommand(accountId, new DateOnly(2026, 11, 25), 1500m, "Salário Nov", null), ct),
            showErrorSnackbar: false);
        await Executor.ExecuteAsync<RegisterDirectExpenseHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterDirectExpenseCommand(accountId, new DateOnly(2026, 11, 10), 200m, "Mercado", SystemCategories.FoodId), ct),
            showErrorSnackbar: false);
        await Executor.ExecuteAsync<RegisterDirectExpenseHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterDirectExpenseCommand(accountId, new DateOnly(2026, 11, 22), 100m, "Consulta futura", SystemCategories.HealthId), ct),
            showErrorSnackbar: false);
        await Executor.ExecuteAsync<RegisterFixedExpenseHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterFixedExpenseCommand(accountId, 2026, 11, 2026, 11, 500m, "Internet", SystemCategories.ServicesId), ct),
            showErrorSnackbar: false);

        // Card purchase on Nov 10 (rule 25/5 -> closes Nov 25, due Dec 5 = 300)
        await Executor.ExecuteAsync<RegisterCreditCardPurchaseHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterCreditCardPurchaseCommand(
                accountId, cardId, new DateOnly(2026, 11, 10), 300m, 1, 1,
                SystemPurchaseConditions.OwnId, "Passagem", SystemCategories.TransportId), ct),
            showErrorSnackbar: false);

        var cut = Render<DashboardPage>();

        // RealBalance on Nov 15 = 2000 + 1000 - 200 = 2800
        // Nov Surplus: TotalAvailable = 2800 + 1500 = 4300; Discounts = 0 (invoice due Dec) + 500 (fixed) + 100 (future direct) = 600; Surplus = 3700
        var state = cut.Instance.State;
        Assert.NotNull(state.MonthlySurplus);
        Assert.Equal(2800m, state.MonthlySurplus!.SelectedMonth.StartingCashBalance);
        Assert.Equal(4300m, state.MonthlySurplus.SelectedMonth.TotalAvailable);
        Assert.Equal(0m, state.MonthlySurplus.SelectedMonth.CardInvoiceDue);
        Assert.Equal(500m, state.MonthlySurplus.SelectedMonth.FixedExpenses);
        Assert.Equal(100m, state.MonthlySurplus.SelectedMonth.DirectExpenses);
        Assert.True(state.MonthlySurplus.SelectedMonth.DirectExpensesAreFutureOnly);
        Assert.Equal(3700m, state.MonthlySurplus.SelectedMonth.ProjectedSurplus);

        // Series includes historical past (Oct), current (Nov), and 5 future months (Dec..Apr) = 7 points + AverageMonthlyVariation
        Assert.Equal(7, state.MonthlySurplus.Series.Count);
        Assert.True(state.MonthlySurplus.Series[0].IsHistorical);
        Assert.Equal(3000m, state.MonthlySurplus.Series[0].ProjectedSurplus);
        Assert.True(state.MonthlySurplus.Series[1].IsCurrentMonth);
        Assert.Equal(3400m, state.MonthlySurplus.Series[2].ProjectedSurplus); // Dec rollover: 3700 - 300 (due Dec 5)
        Assert.Equal(state.MonthlySurplus.AverageMonthlyVariation, Math.Round((1000m + 700m - 300m) / 7m, 2, MidpointRounding.AwayFromZero));

        Assert.Contains("3.700,00", cut.Find("[data-testid='monthly-surplus-value']").TextContent);
        Assert.Contains("4.300,00", cut.Find("[data-testid='monthly-surplus-total-available']").TextContent);
        Assert.Contains("futuros", cut.Find("[data-testid='monthly-surplus-direct-label']").TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("200,00", cut.Find("[data-testid='monthly-surplus-average-variation']").TextContent);

        // Navigating to past month (Oct 2026) clears the future-only flag
        await cut.Find("[data-testid='month-prev']").ClickAsync();
        Assert.False(cut.Instance.State.MonthlySurplus!.SelectedMonth.DirectExpensesAreFutureOnly);
    }

    [Fact]
    public async Task Dashboard_GlobalConditionFilter_PropagatesToSurplusDueDateInvoiceProjectedResultAndSpending_WithoutAlteringRealBalance()
    {
        Clock.Today = new DateOnly(2026, 11, 15);
        var accountId = await SeedAccountAsync(initialBalance: 3000m, initialBalanceDate: new DateOnly(2026, 10, 1));
        var cardId = await SeedCreditCardAsync(accountId, closingDay: 25, dueDay: 5, name: "Nubank", limit: 8000m);

        var momCondition = await Executor.ExecuteAsync<CreatePurchaseConditionHandler, PurchaseConditionDto>(
            (h, ct) => h.ExecuteAsync(new CreatePurchaseConditionCommand("Mãe"), ct),
            showErrorSnackbar: false);

        // Settled invoice due Nov 5 (<= today) of 400 ("Outros") -> already left RealBalance (3000 - 400 = 2600)
        await Executor.ExecuteAsync<RegisterCreditCardPurchaseHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterCreditCardPurchaseCommand(
                accountId, cardId, new DateOnly(2026, 10, 10), 400m, 1, 1,
                SystemPurchaseConditions.OthersId, "Out Outros", SystemCategories.ShoppingId), ct),
            showErrorSnackbar: false);

        // Purchases closing Nov 25, DUE Dec 5: Own 200 + Mom 500 = 700
        await Executor.ExecuteAsync<RegisterCreditCardPurchaseHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterCreditCardPurchaseCommand(
                accountId, cardId, new DateOnly(2026, 11, 10), 200m, 1, 1,
                SystemPurchaseConditions.OwnId, "Meu item", SystemCategories.ShoppingId), ct),
            showErrorSnackbar: false);
        await Executor.ExecuteAsync<RegisterCreditCardPurchaseHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterCreditCardPurchaseCommand(
                accountId, cardId, new DateOnly(2026, 11, 11), 500m, 1, 1,
                momCondition.Value!.Id, "Presente Mãe", SystemCategories.ShoppingId), ct),
            showErrorSnackbar: false);

        var cut = Render<DashboardPage>();

        // In Nov 2026:
        // - RealBalance = 2600 (unfiltered)
        // - ProjectedResult (accrual, ClosingMonth Nov) = -700 (all conditions)
        // - CategorySpending (ClosingMonth Nov) = 700
        // - InvoiceSummary (DueMonth Nov -> due Nov 5) = 400 (Outros!)
        Assert.Contains("2.600,00", cut.Find("[data-testid='real-balance-value']").TextContent);
        Assert.Equal(HearthFormat.Money(-700m), cut.Find("[data-testid='projected-result-value']").TextContent.Trim());
        Assert.Contains("400,00", cut.Find("[data-testid='dashboard-invoice-calculated']").TextContent);

        // Select "Própria" globally -> RealBalance stays 2600, ProjectedResult becomes -200, Spending becomes 200, Nov DueDate invoice becomes 0
        await cut.Find("[data-testid='dashboard-condition-own']").ClickAsync();
        Assert.Equal(SystemPurchaseConditions.OwnId, cut.Instance.State.SelectedConditionId);
        Assert.Contains("2.600,00", cut.Find("[data-testid='real-balance-value']").TextContent);
        Assert.Equal(HearthFormat.Money(-200m), cut.Find("[data-testid='projected-result-value']").TextContent.Trim());
        Assert.Contains("200,00", cut.Find("[data-testid='spending-total']").TextContent);
        Assert.Contains("0,00", cut.Find("[data-testid='dashboard-invoice-calculated']").TextContent);
        Assert.Equal(SystemPurchaseConditions.OwnId, cut.Instance.State.ProjectedResult!.ConditionId);
        Assert.Equal(SystemPurchaseConditions.OwnId, cut.Instance.State.InvoiceSummary!.ConditionId);
        Assert.Equal(SystemPurchaseConditions.OwnId, cut.Instance.State.MonthlySurplus!.ConditionId);

        // Navigate to Dec 2026 (while "Própria" filter stays active!):
        // - DueMonth Dec has closing Nov 25, due Dec 5 -> Own = 200, Surplus = 2600 - 200 = 2400
        await cut.Find("[data-testid='month-next']").ClickAsync();
        Assert.Contains("200,00", cut.Find("[data-testid='dashboard-invoice-calculated']").TextContent);
        Assert.Contains("2.400,00", cut.Find("[data-testid='monthly-surplus-value']").TextContent);
        Assert.Contains("2.600,00", cut.Find("[data-testid='real-balance-value']").TextContent);

        // Switch back to "Todas" on Dec 2026 -> DueMonth Dec invoice becomes 700, Surplus becomes 2600 - 700 = 1900, RealBalance stays 2600
        await cut.Find("[data-testid='dashboard-condition-all']").ClickAsync();
        Assert.Null(cut.Instance.State.SelectedConditionId);
        Assert.Contains("700,00", cut.Find("[data-testid='dashboard-invoice-calculated']").TextContent);
        Assert.Contains("1.900,00", cut.Find("[data-testid='monthly-surplus-value']").TextContent);
        Assert.Contains("2.600,00", cut.Find("[data-testid='real-balance-value']").TextContent);
    }

    [Fact]
    public async Task Dashboard_ConditionOverflow_ShowsPlusCountPopoverAndPromotesSelectedHiddenConditionToVisibleBar()
    {
        await SeedAccountAsync(initialBalance: 1000m);

        // 2 system conditions ("Própria", "Outros") + 4 custom conditions = 6 total -> 4 visible + "+2" overflow
        foreach (var name in new[] { "Ana", "Mãe", "Trabalho", "Viagem" })
        {
            await Executor.ExecuteAsync<CreatePurchaseConditionHandler, PurchaseConditionDto>(
                (h, ct) => h.ExecuteAsync(new CreatePurchaseConditionCommand(name), ct),
                showErrorSnackbar: false);
        }

        var cut = Render<DashboardPage>();

        var overflowBtn = cut.Find("[data-testid='dashboard-condition-overflow-button']");
        Assert.Equal("+2", overflowBtn.TextContent.Trim());
        Assert.Empty(cut.FindAll("[data-testid='dashboard-condition-overflow-menu']"));
        Assert.Empty(cut.FindAll("[data-testid='dashboard-condition-custom-Viagem']"));

        // Open overflow popover and select hidden condition "Viagem"
        await overflowBtn.ClickAsync();
        Assert.NotEmpty(cut.FindAll("[data-testid='dashboard-condition-overflow-menu']"));
        await cut.Find("[data-testid='dashboard-condition-custom-Viagem']").ClickAsync();

        // Popover closes, "Viagem" is promoted into the visible bar as active, and RealBalance shows unfiltered transparency note
        Assert.Empty(cut.FindAll("[data-testid='dashboard-condition-overflow-menu']"));
        var promotedPill = cut.Find("[data-testid='dashboard-condition-custom-Viagem']");
        Assert.Equal("true", promotedPill.GetAttribute("aria-pressed"));
        Assert.NotNull(cut.Instance.State.SelectedConditionId);
        Assert.NotEmpty(cut.FindAll("[data-testid='real-balance-unfiltered-note']"));
    }

    [Fact]
    public async Task Dashboard_HeroCards_SupportQuickInitialBalanceEditAndEvolutionChartNavigation()
    {
        Clock.Today = new DateOnly(2026, 11, 15);
        await SeedAccountAsync(initialBalance: 1000m, initialBalanceDate: new DateOnly(2026, 10, 1));

        var cut = Render<DashboardPage>();

        // Evolution chart distinguishes historical and projection paths and allows selecting a month directly
        Assert.NotEmpty(cut.FindAll("[data-testid='surplus-evolution-chart']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='chart-historical-path']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='chart-projection-path']"));

        await cut.Find("[data-testid='chart-point-2026-12']").ClickAsync();
        Assert.Contains("Dezembro de 2026", cut.Find("[data-testid='month-label']").TextContent);

        // Discreet inline quick edit of initial balance updates both RealBalance and MonthlySurplus
        await cut.Find("[data-testid='edit-initial-balance-button']").ClickAsync();
        cut.Find("[data-testid='inline-initial-balance-input']").Input("2500");
        await cut.Find("[data-testid='save-initial-balance-button']").ClickAsync();

        Assert.Contains("2.500,00", cut.Find("[data-testid='real-balance-value']").TextContent);
        Assert.Contains("2.500,00", cut.Find("[data-testid='monthly-surplus-value']").TextContent);

        // Progressive disclosure toggles expand full cascade / composition rows on demand
        await cut.Find("[data-testid='toggle-surplus-details']").ClickAsync();
        Assert.Equal("true", cut.Find("[data-testid='toggle-surplus-details']").GetAttribute("aria-expanded"));
        await cut.Find("[data-testid='toggle-real-balance-details']").ClickAsync();
        Assert.Equal("true", cut.Find("[data-testid='toggle-real-balance-details']").GetAttribute("aria-expanded"));
    }

    [Fact]
    public async Task Dashboard_EvolutionChart_ColorsSegmentsNodesAndPillsByAmplitudeMidpointWithoutAlteringAverageVariation()
    {
        Clock.Today = new DateOnly(2026, 11, 15);
        var accountId = await SeedAccountAsync(initialBalance: 1000m, initialBalanceDate: new DateOnly(2026, 9, 1));

        // Sep (hist): +0 -> 1000
        // Oct (hist): +1000 -> 2000
        await Executor.ExecuteAsync<RegisterIncomeHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterIncomeCommand(accountId, new DateOnly(2026, 10, 5), 1000m, "Out", new DateOnly(2026, 10, 5)), ct),
            showErrorSnackbar: false);
        // Nov (current): -500 future direct -> 1500
        await Executor.ExecuteAsync<RegisterDirectExpenseHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterDirectExpenseCommand(accountId, new DateOnly(2026, 11, 25), 500m, "Nov", SystemCategories.ServicesId), ct),
            showErrorSnackbar: false);
        // Dec (future): +2500 expected -> 4000 (maxVal = 4000, minVal = 1000 -> midpoint = (4000 - 1000) / 2 = 1500.00!)
        await Executor.ExecuteAsync<RegisterIncomeHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterIncomeCommand(accountId, new DateOnly(2026, 12, 10), 2500m, "Bônus Dez", null), ct),
            showErrorSnackbar: false);

        // Series closing values: Sep(1000), Oct(2000), Nov(1500), Dec(4000), Jan(4000), Feb(4000), Mar(4000), Apr(4000)
        // AverageMonthlyVariation = (0 + 1000 - 500 + 2500) / 8 = 3000 / 8 = +375.00 (preserved untouched!)
        // Amplitude midpoint = (4000 - 1000) / 2 = 1500.00:
        // - Sep (1000 <= 1500): red/salmon (is-below-mid)
        // - Oct (2000 > 1500): green (is-above-mid) + historical solid (is-historical)
        // - Nov (1500 <= 1500, exact boundary): red/salmon (is-below-mid) + projection dashed (is-projection)
        // - Dec (4000 > 1500): green (is-above-mid) + projection dashed (is-projection)
        var cut = Render<DashboardPage>();

        Assert.Equal(375.00m, cut.Instance.State.MonthlySurplus!.AverageMonthlyVariation);
        Assert.Contains("375,00", cut.Find("[data-testid='monthly-surplus-average-variation']").TextContent);
        Assert.Equal("1500.00", cut.Find(".hearth-evolution-svg").GetAttribute("data-midpoint"));

        var sepNode = cut.Find("circle[data-node='2026-09']");
        Assert.Contains("is-below-mid", sepNode.ClassList);

        var octSegment = cut.Find("path[data-segment='2026-10']");
        Assert.Contains("is-historical", octSegment.ClassList);
        Assert.Contains("is-above-mid", octSegment.ClassList);

        var novSegment = cut.Find("path[data-segment='2026-11']");
        Assert.Contains("is-projection", novSegment.ClassList);
        Assert.Contains("is-below-mid", novSegment.ClassList);

        var decSegment = cut.Find("path[data-segment='2026-12']");
        Assert.Contains("is-projection", decSegment.ClassList);
        Assert.Contains("is-above-mid", decSegment.ClassList);

        // Bottom month pills & selected pill reflect the exact midpoint threshold
        Assert.Contains("is-below-mid", cut.Find("[data-testid='chart-point-2026-11']").ClassList);
        Assert.Contains("is-selected", cut.Find("[data-testid='chart-point-2026-11']").ClassList);
        Assert.Contains("is-above-mid", cut.Find("[data-testid='chart-point-2026-12']").ClassList);

        var legendText = cut.Find(".hearth-evolution-legend").TextContent;
        Assert.Contains("Acima do ponto médio", legendText);
        Assert.Contains("Igual ou abaixo do ponto médio", legendText);
        Assert.Contains("Histórico consolidado", legendText);
        Assert.Contains("Projeção", legendText);
    }

    [Fact]
    public async Task Dashboard_EvolutionChart_WhenAllPointsAreEqual_HandlesFlatSeriesConsistently()
    {
        Clock.Today = new DateOnly(2026, 11, 15);
        await SeedAccountAsync(initialBalance: 1000m, initialBalanceDate: new DateOnly(2026, 10, 1));

        var cut = Render<DashboardPage>();

        // maxVal == minVal == 1000 -> midpoint == 0.00 -> all positive flat points are consistently above midpoint (green)
        Assert.Equal("0.00", cut.Find(".hearth-evolution-svg").GetAttribute("data-midpoint"));
        Assert.Contains("is-above-mid", cut.Find("path[data-segment='2026-11']").ClassList);
        Assert.Contains("is-above-mid", cut.Find("circle[data-node='2026-11']").ClassList);
    }
}
