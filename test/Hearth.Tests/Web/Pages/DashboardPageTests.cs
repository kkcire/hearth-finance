using Bunit;
using Hearth.Application.CategoryBudgets.CreateCategoryBudget;
using Hearth.Application.CreditCardPurchases.RegisterCreditCardPurchase;
using Hearth.Application.DirectExpenses.RegisterDirectExpense;
using Hearth.Application.FixedExpenses.RegisterFixedExpense;
using Hearth.Application.Incomes.RegisterIncome;
using Hearth.Application.PurchaseConditions;
using Hearth.Application.PurchaseConditions.CreatePurchaseCondition;
using Hearth.Application.Reports.GetMonthlyDashboard;
using Hearth.Domain;
using Hearth.Domain.ValueObjects;
using Hearth.Web.Components.Pages;
using Hearth.Web.Components.Shared;
using Hearth.Web.Services;

namespace Hearth.Tests.Web.Pages;

public sealed class DashboardPageTests : WebTestBase
{
    [Fact]
    public async Task Dashboard_RendersAllThreeRowsWithIsolatedMonthlyResultAndNextMonthEstimate()
    {
        Clock.Today = new DateOnly(2026, 11, 24);
        var accountId = await SeedAccountAsync(initialBalance: 1000m, initialBalanceDate: new DateOnly(2026, 10, 1));
        var cardId = await SeedCreditCardAsync(accountId, closingDay: 2, dueDay: 14, name: "Cartão BB", limit: 7000m);

        // Nov realized income (+500, Date <= today) + Nov expected income (+1500, Date > today) => RealBalance = 1500, MonthlyIncome(Nov) = 2000
        await Executor.ExecuteAsync<RegisterIncomeHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterIncomeCommand(accountId, new DateOnly(2026, 11, 5), 500m, "Freela"), ct),
            showErrorSnackbar: false);
        await Executor.ExecuteAsync<RegisterIncomeHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterIncomeCommand(accountId, new DateOnly(2026, 11, 28), 1500m, "Salário"), ct),
            showErrorSnackbar: false);

        // Nov direct expense (-300) => RealBalance = 1200, Expenses(Nov) = 300 -> Result = 1700 -> SavingsRate = 85.0%
        await Executor.ExecuteAsync<RegisterDirectExpenseHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterDirectExpenseCommand(accountId, new DateOnly(2026, 11, 10), 300m, "Supermercado", SystemCategories.FoodId), ct),
            showErrorSnackbar: false);

        // Budget on Food = 200 (exceeded by 300 => 150%)
        await Executor.ExecuteAsync<CreateCategoryBudgetHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new CreateCategoryBudgetCommand(accountId, SystemCategories.FoodId, 2026, 11, null, null, 200m), ct),
            showErrorSnackbar: false);

        // Card purchase on Nov 10 (3x 100 = 300, due Dec 14, Jan 14, Feb 14)
        // On Nov 24 (Nov 14 already paid), Fatura Atual resolves to open Dec 14 cycle (100) + future (200)
        await Executor.ExecuteAsync<RegisterCreditCardPurchaseHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterCreditCardPurchaseCommand(
                accountId, cardId, new DateOnly(2026, 11, 10), 300m, 3, 1,
                SystemPurchaseConditions.OwnId, "Monitor", SystemCategories.ShoppingId), ct),
            showErrorSnackbar: false);

        // Dec income (+2200) -> NextMonthEstimate(Dec): 2200 - 100 (Dec installment) = 2100
        // OpeningBalance(Dec) = RealBalance(1200) + pending Nov income(1500) = 2700 -> EndingBalance(Dec) = 2700 + 2100 = 4800
        await Executor.ExecuteAsync<RegisterIncomeHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterIncomeCommand(accountId, new DateOnly(2026, 12, 5), 2200m, "Salário Dez"), ct),
            showErrorSnackbar: false);

        var cut = Render<DashboardPage>();

        // All 3 rows render cleanly
        Assert.NotEmpty(cut.FindAll("[data-testid='dashboard-row-1']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='dashboard-row-2']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='dashboard-row-3']"));

        // Row 1 checks — SALDO has NO "Dinheiro real em conta" subtitle
        var balanceCardText = cut.Find("[data-testid='dashboard-balance-card']").TextContent;
        Assert.Contains("1.200,00", cut.Find("[data-testid='real-balance-value']").TextContent);
        Assert.DoesNotContain("Dinheiro real em conta", balanceCardText);

        // Fatura Atual resolves to active open Dec 14 cycle on Nov 24 — rendered in neutral primary tone (NOT red danger!)
        var invoiceAmountEl = cut.Find("[data-testid='dashboard-invoice-amount']");
        Assert.Contains("100,00", invoiceAmountEl.TextContent);
        Assert.Contains("is-primary", invoiceAmountEl.ClassList);
        Assert.DoesNotContain("is-danger", invoiceAmountEl.ClassList);
        Assert.Contains("Aberta", cut.Find("[data-testid='dashboard-invoice-status']").TextContent);
        Assert.Contains("200,00", cut.Find("[data-testid='dashboard-future-installments']").TextContent);

        Assert.Contains("2.000,00", cut.Find("[data-testid='dashboard-monthly-income']").TextContent);
        Assert.Contains("300,00", cut.Find("[data-testid='dashboard-monthly-expenses']").TextContent);
        Assert.Contains("1.700,00", cut.Find("[data-testid='dashboard-monthly-result-value']").TextContent);

        // Interactive 3-in-1 morphing flow card: clicking Receita turns whole card into Receita composition;
        // clicking Despesa turns whole card into Despesa composition; Voltar restores 2x1 summary
        await cut.Find("[data-testid='flow-open-income']").ClickAsync();
        var incomeView = cut.Find("[data-testid='flow-income-view']");
        Assert.Contains("2.000,00", incomeView.QuerySelector("[data-testid='flow-comp-income']")!.TextContent);
        Assert.Contains("Salário", incomeView.TextContent);

        await cut.Find("[data-testid='flow-back-summary']").ClickAsync();
        await cut.Find("[data-testid='flow-open-expenses']").ClickAsync();
        var expensesView = cut.Find("[data-testid='flow-expenses-view']");
        Assert.Contains("300,00", expensesView.QuerySelector("[data-testid='flow-comp-direct']")!.TextContent);
        await cut.Find("[data-testid='flow-back-summary']").ClickAsync();

        // Row 2 checks (Net monthly result bar chart against R$ 0 break-even + no duplicate break-even badge + CategorySpendingDonut)
        Assert.NotEmpty(cut.FindAll("[data-testid='monthly-result-chart']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='chart-breakeven-line']"));
        Assert.Empty(cut.FindAll(".hearth-breakeven-badge"));
        var novBar = cut.Find("[data-bar='2026-11']");
        Assert.Contains("is-healthy", novBar.ClassList);
        Assert.DoesNotContain("is-deficit", novBar.ClassList);
        Assert.Contains("300,00", cut.Find("[data-testid='spending-total']").TextContent);
        Assert.NotEmpty(cut.FindAll("[data-testid='budget-exceeded-badge']"));

        // Clicking a category in Gastos por categoria expands its itemized composition
        Assert.Empty(cut.FindAll("[data-testid='category-composition-panel']"));
        await cut.Find($"[data-testid='category-toggle-{SystemCategories.FoodId}']").ClickAsync();
        var catPanel = cut.Find("[data-testid='category-composition-panel']");
        Assert.Contains("Alimentação", catPanel.TextContent);
        Assert.Contains("Supermercado", catPanel.TextContent);
        Assert.Contains("Gasto direto", catPanel.TextContent);
        Assert.Contains("300,00", catPanel.TextContent);
        await cut.Find("[data-testid='close-category-composition']").ClickAsync();
        Assert.Empty(cut.FindAll("[data-testid='category-composition-panel']"));

        // Empty Oct 2026 bar is NoActivity (neutral empty class, NEVER orange warning!)
        var octBar = cut.Find("[data-bar='2026-10']");
        Assert.Equal("NoActivity", octBar.GetAttribute("data-status"));
        Assert.Contains("is-empty", octBar.ClassList);
        Assert.DoesNotContain("is-warning", octBar.ClassList);

        // Row 3 checks (Recent Activities -> /atividades, Savings Rate Ring + explanation, Redesigned Next Month Estimate)
        var activitiesText = cut.Find("[data-testid='recent-activities-list']").TextContent;
        Assert.Contains("Supermercado", activitiesText);
        Assert.Contains("Monitor (3x)", activitiesText);
        Assert.DoesNotContain("Freela", activitiesText);
        Assert.Equal("/atividades", cut.Find("[data-testid='recent-activities-view-all']").GetAttribute("href"));

        Assert.Contains("85,0%", cut.Find("[data-testid='savings-rate-value']").TextContent);
        Assert.Empty(cut.FindAll("[data-testid='savings-rate-explanation']"));
        await cut.Find("[data-testid='savings-rate-info-toggle']").ClickAsync();
        var explanation = cut.Find("[data-testid='savings-rate-explanation']");
        Assert.Contains("Renda Própria", explanation.TextContent);
        Assert.Contains("reembolsos", explanation.TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("independência financeira", explanation.TextContent);
        Assert.Contains("Saudável", explanation.TextContent);

        Assert.Contains("Dezembro de 2026", cut.Find("[data-testid='next-month-badge']").TextContent);
        Assert.NotEmpty(cut.FindAll("[data-testid='next-month-commitment-bar']"));
        Assert.Contains("2.200,00", cut.Find("[data-testid='next-month-income']").TextContent);
        Assert.Contains("100,00", cut.Find("[data-testid='next-month-installments']").TextContent);
        Assert.Contains("100,00", cut.Find("[data-testid='next-month-expenses']").TextContent);
        Assert.Contains("2.100,00", cut.Find("[data-testid='next-month-projected-result']").TextContent);
        Assert.Contains("95,5%", cut.Find("[data-testid='next-month-savings-rate']").TextContent);
        Assert.Contains("2.700,00", cut.Find("[data-testid='next-month-opening-balance']").TextContent);
        Assert.Contains("4.800,00", cut.Find("[data-testid='next-month-ending-balance']").TextContent);

        // Navigating to Dec 2026 updates isolated Monthly Result while keeping Real Balance (1200) untouched
        // and omits cash balance projection (computed only from current calendar month)
        await cut.Find("[data-testid='month-next']").ClickAsync();
        Assert.Contains("Dezembro de 2026", cut.Find("[data-testid='month-label']").TextContent);
        Assert.Contains("1.200,00", cut.Find("[data-testid='real-balance-value']").TextContent);
        Assert.Contains("100,00", cut.Find("[data-testid='dashboard-invoice-amount']").TextContent);
        Assert.Contains("200,00", cut.Find("[data-testid='dashboard-future-installments']").TextContent);
        Assert.Contains("2.100,00", cut.Find("[data-testid='dashboard-monthly-result-value']").TextContent);
        Assert.Empty(cut.FindAll("[data-testid='next-month-balance-projection']"));
    }

    [Fact]
    public async Task Dashboard_EmptyMonth_RendersCleanEmptyStatesAcrossCardDonutAndActivities()
    {
        await SeedAccountAsync(initialBalance: 0m);

        var cut = Render<DashboardPage>();

        Assert.Equal("Painel do Mês", cut.Find("h1").TextContent.Trim());
        Assert.Equal("Mês anterior", cut.Find("[data-testid='month-prev']").GetAttribute("aria-label"));
        Assert.Equal("Próximo mês", cut.Find("[data-testid='month-next']").GetAttribute("aria-label"));
        Assert.NotEmpty(cut.FindAll("[data-testid='dashboard-no-card-state']"));
        Assert.Equal("/cartao", cut.Find("[data-testid='dashboard-configure-card-link']").GetAttribute("href"));
        Assert.NotEmpty(cut.FindAll("[data-testid='category-spending-empty-state']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='recent-activities-empty-state']"));
        Assert.Equal("—", cut.Find("[data-testid='savings-rate-value']").TextContent.Trim());
        Assert.Equal("Sem dados", cut.Find("[data-testid='savings-rate-status-cue']").TextContent.Trim());
        Assert.Empty(cut.FindAll(".hearth-savings-ring-progress"));
    }

    [Fact]
    public async Task Dashboard_Option1SavingsRate_IgnoresThirdPartyReimbursementDilutionAndShowsNeutralWhenNoOwnIncome()
    {
        Clock.Today = new DateOnly(2026, 11, 24);
        var accountId = await SeedAccountAsync(initialBalance: 0m, initialBalanceDate: new DateOnly(2026, 10, 1));
        var cardId = await SeedCreditCardAsync(accountId, closingDay: 2, dueDay: 14, name: "Cartão", limit: 5000m);

        var paiResult = await Executor.ExecuteAsync<CreatePurchaseConditionHandler, PurchaseConditionDto>(
            (h, ct) => h.ExecuteAsync(new CreatePurchaseConditionCommand("Pai"), ct),
            showErrorSnackbar: false);
        var paiId = paiResult.Value!.Id;

        // R$ 2.000 own income + R$ 950 third-party reimbursement (Pai)
        await Executor.ExecuteAsync<RegisterIncomeHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterIncomeCommand(accountId, new DateOnly(2026, 11, 5), 2000m, "Salário", SystemPurchaseConditions.OwnId), ct),
            showErrorSnackbar: false);
        await Executor.ExecuteAsync<RegisterIncomeHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterIncomeCommand(accountId, new DateOnly(2026, 11, 10), 950m, "Reembolso Pai", paiId), ct),
            showErrorSnackbar: false);

        // R$ 300 own expense + R$ 950 Pai card expense due Nov 14
        await Executor.ExecuteAsync<RegisterDirectExpenseHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterDirectExpenseCommand(accountId, new DateOnly(2026, 11, 8), 300m, "Mercado", SystemCategories.FoodId), ct),
            showErrorSnackbar: false);
        await Executor.ExecuteAsync<RegisterCreditCardPurchaseHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterCreditCardPurchaseCommand(
                accountId, cardId, new DateOnly(2026, 10, 15), 950m, 1, 1,
                paiId, "Compra Pai", SystemCategories.ShoppingId), ct),
            showErrorSnackbar: false);

        var cut = Render<DashboardPage>();

        // Under "Todas": gross movement visible (2.950 / 1.250 / 1.700), Taxa de Poupança = 85,0% (NOT 57,6%)
        Assert.Contains("2.950,00", cut.Find("[data-testid='dashboard-monthly-income']").TextContent);
        Assert.Contains("1.250,00", cut.Find("[data-testid='dashboard-monthly-expenses']").TextContent);
        Assert.Contains("1.700,00", cut.Find("[data-testid='dashboard-monthly-result-value']").TextContent);
        Assert.Equal("85,0%", cut.Find("[data-testid='savings-rate-value']").TextContent.Trim());
        Assert.Equal("Saudável", cut.Find("[data-testid='savings-rate-status-cue']").TextContent.Trim());

        // Under "Própria": Receita 2.000 / Despesa 300 / Resultado 1.700 / Taxa 85,0%
        await cut.Find("[data-testid='dashboard-condition-own']").ClickAsync();
        Assert.Contains("2.000,00", cut.Find("[data-testid='dashboard-monthly-income']").TextContent);
        Assert.Contains("300,00", cut.Find("[data-testid='dashboard-monthly-expenses']").TextContent);
        Assert.Contains("1.700,00", cut.Find("[data-testid='dashboard-monthly-result-value']").TextContent);
        Assert.Equal("85,0%", cut.Find("[data-testid='savings-rate-value']").TextContent.Trim());

        // Under "Pai" (OwnMonthlyIncome == 0): neutral "—" / "Sem dados", NEVER falsely "Atenção"
        await cut.Find("[data-testid='dashboard-condition-custom-Pai']").ClickAsync();
        Assert.Contains("950,00", cut.Find("[data-testid='dashboard-monthly-income']").TextContent);
        Assert.Equal("—", cut.Find("[data-testid='savings-rate-value']").TextContent.Trim());
        Assert.Equal("Sem dados", cut.Find("[data-testid='savings-rate-status-cue']").TextContent.Trim());
        Assert.Empty(cut.FindAll(".hearth-savings-ring-progress"));
    }

    [Fact]
    public async Task Dashboard_ConditionFilterAndOverflow_FilterCardDataWithoutDistortingRealBalance()
    {
        Clock.Today = new DateOnly(2026, 11, 24);
        var accountId = await SeedAccountAsync(initialBalance: 2000m, initialBalanceDate: new DateOnly(2026, 10, 1));
        var cardId = await SeedCreditCardAsync(accountId, closingDay: 2, dueDay: 14, name: "Cartão", limit: 8000m);

        // 2 system + 4 custom conditions = 6 total -> 4 visible + "+2" overflow
        PurchaseConditionDto? viagem = null;
        foreach (var name in new[] { "Ana", "Mãe", "Trabalho", "Viagem" })
        {
            var created = await Executor.ExecuteAsync<CreatePurchaseConditionHandler, PurchaseConditionDto>(
                (h, ct) => h.ExecuteAsync(new CreatePurchaseConditionCommand(name), ct),
                showErrorSnackbar: false);
            if (name == "Viagem")
            {
                viagem = created.Value;
            }
        }

        // Purchases due Dec 14: Own 200 + Viagem 500
        await Executor.ExecuteAsync<RegisterCreditCardPurchaseHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterCreditCardPurchaseCommand(
                accountId, cardId, new DateOnly(2026, 11, 10), 200m, 1, 1,
                SystemPurchaseConditions.OwnId, "Meu item", SystemCategories.ShoppingId), ct),
            showErrorSnackbar: false);
        await Executor.ExecuteAsync<RegisterCreditCardPurchaseHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterCreditCardPurchaseCommand(
                accountId, cardId, new DateOnly(2026, 11, 11), 500m, 1, 1,
                viagem!.Id, "Passagem", SystemCategories.TransportId), ct),
            showErrorSnackbar: false);

        var cut = Render<DashboardPage>();
        await cut.Find("[data-testid='month-next']").ClickAsync(); // Dec 2026

        Assert.Contains("700,00", cut.Find("[data-testid='dashboard-invoice-amount']").TextContent);

        // Open "+2" overflow and pick "Viagem"
        var overflowBtn = cut.Find("[data-testid='dashboard-condition-overflow-button']");
        Assert.Equal("+2", overflowBtn.TextContent.Trim());
        await overflowBtn.ClickAsync();
        await cut.Find("[data-testid='dashboard-condition-custom-Viagem']").ClickAsync();

        Assert.Contains("500,00", cut.Find("[data-testid='dashboard-invoice-amount']").TextContent);
        Assert.Contains("2.000,00", cut.Find("[data-testid='real-balance-value']").TextContent);
        Assert.NotEmpty(cut.FindAll("[data-testid='real-balance-unfiltered-note']"));
    }

    [Fact]
    public async Task Dashboard_QuickLaunchMenu_OpensAndExposesAllFourTransactionRoutes()
    {
        await SeedAccountAsync();
        var cut = Render<DashboardPage>();

        Assert.Equal("true", cut.Find("[data-testid='quick-launch-menu']").GetAttribute("aria-hidden"));

        await cut.Find("[data-testid='quick-launch-trigger']").ClickAsync();

        var menu = cut.Find("[data-testid='quick-launch-menu']");
        Assert.Equal("false", menu.GetAttribute("aria-hidden"));
        Assert.Equal("/gastos-diretos", menu.QuerySelector("[data-testid='quick-launch-direct']")!.GetAttribute("href"));
        Assert.Equal("/cartao?aba=compras", menu.QuerySelector("[data-testid='quick-action-card-purchase']")!.GetAttribute("href"));
        Assert.Equal("/receitas", menu.QuerySelector("[data-testid='quick-launch-income']")!.GetAttribute("href"));
        Assert.Equal("/despesas-fixas", menu.QuerySelector("[data-testid='quick-launch-fixed']")!.GetAttribute("href"));
    }

    [Fact]
    public async Task Dashboard_BalanceCard_SupportsDirectSetBalanceAndCompositionHistoryWithoutAffectingCompetence()
    {
        Clock.Today = new DateOnly(2026, 11, 20);
        await SeedAccountAsync(initialBalance: 1000m, initialBalanceDate: new DateOnly(2026, 10, 15));
        var cut = Render<DashboardPage>();

        // "Saldo inicial" edits the single Day-1 opening balance without any mid-month date picker
        Assert.Empty(cut.FindAll("[data-testid='edit-initial-balance-button']"));

        await cut.Find("[data-testid='set-balance-button']").ClickAsync();
        Assert.Empty(cut.FindAll("input[data-testid='set-balance-date-input']"));
        cut.Find("input[data-testid='set-balance-amount-input']").Input("1500");
        cut.Find("input[data-testid='set-balance-reason-input']").Input("Conferência extrato");
        await cut.Find("[data-testid='save-set-balance-button']").ClickAsync();

        // SALDO recalculates from Day-1 balance R$ 1.500,00 while Monthly Income & Result remain R$ 0,00
        Assert.Contains("1.500,00", cut.Find("[data-testid='real-balance-value']").TextContent);
        Assert.Contains("0,00", cut.Find("[data-testid='dashboard-monthly-income']").TextContent);
        Assert.Contains("0,00", cut.Find("[data-testid='dashboard-monthly-result-value']").TextContent);

        await cut.Find("[data-testid='toggle-real-balance-details']").ClickAsync();
        var drawer = cut.Find("[data-testid='balance-composition-drawer']");
        Assert.Contains("1.500,00", drawer.QuerySelector("[data-testid='balance-composition-initial']")!.TextContent);
        Assert.Contains("Conferência extrato", drawer.QuerySelector("[data-testid='balance-composition-history-log']")!.TextContent);

        // Deleting the balance entry removes it from history and resets SALDO cleanly to R$ 0,00
        var deleteBtn = drawer.QuerySelector("[data-testid^='delete-balance-entry-']")!;
        var entryId = deleteBtn.GetAttribute("data-testid")!.Replace("delete-balance-entry-", string.Empty);
        await cut.Find($"[data-testid='delete-balance-entry-{entryId}']").ClickAsync();
        await cut.Find($"[data-testid='confirm-delete-balance-entry-{entryId}']").ClickAsync();

        Assert.Contains("0,00", cut.Find("[data-testid='real-balance-value']").TextContent);
        Assert.Empty(cut.FindAll("[data-testid='balance-composition-history-log']"));
    }

    [Fact]
    public async Task Dashboard_HeaderCalendarDayPicker_UpdatesRealBalanceCutoffDateAndResetsToToday()
    {
        Clock.Today = new DateOnly(2026, 11, 24);
        var accountId = await SeedAccountAsync(initialBalance: 1000m, initialBalanceDate: new DateOnly(2026, 11, 18));
        await Executor.ExecuteAsync<RegisterDirectExpenseHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterDirectExpenseCommand(accountId, new DateOnly(2026, 11, 20), 250m, "Mercado", SystemCategories.FoodId), ct),
            showErrorSnackbar: false);

        var cut = Render<DashboardPage>();

        // Day calendar picker is rendered right beside MonthNavigator in the Dashboard header
        var datePicker = cut.Find("input[data-testid='real-balance-date-input']");
        Assert.Equal("24/11/2026", datePicker.GetAttribute("value"));
        Assert.Empty(cut.FindAll("[data-testid='real-balance-today-button']"));
        Assert.Contains("750,00", cut.Find("[data-testid='real-balance-value']").TextContent);

        // Selecting an earlier day (10/11/2026, before the Nov 20 expense) updates SALDO immediately and shows "Hoje" button
        datePicker.Change("10/11/2026");
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("1.000,00", cut.Find("[data-testid='real-balance-value']").TextContent);
            Assert.Contains("10/11/2026", cut.Find("[data-testid='dashboard-balance-card']").TextContent);
            Assert.NotEmpty(cut.FindAll("[data-testid='real-balance-today-button']"));
        });

        // Clicking "Hoje" resets back to today (24/11/2026) and recalculates SALDO = 750,00
        await cut.Find("[data-testid='real-balance-today-button']").ClickAsync();
        Assert.Contains("750,00", cut.Find("[data-testid='real-balance-value']").TextContent);
        Assert.Empty(cut.FindAll("[data-testid='real-balance-today-button']"));
    }

    [Fact]
    public async Task Dashboard_DisablesBackwardNavigationAtTrackingStartPeriod_AndChartExcludesPriorMonths()
    {
        Clock.Today = new DateOnly(2026, 11, 24);
        // Account created Nov 18, 2026 -> TrackingStartPeriod = 2026-11
        await SeedAccountAsync(initialBalance: 500m, initialBalanceDate: new DateOnly(2026, 11, 18));

        var cut = Render<DashboardPage>();

        // Already at Nov 2026 == TrackingStartPeriod -> previous button MUST be disabled
        var prevBtn = cut.Find("[data-testid='month-prev']");
        Assert.True(prevBtn.HasAttribute("disabled"));

        // Chart MUST NOT plot any month prior to 2026-11
        var bars = cut.FindAll("[data-bar]");
        var onlyBar = Assert.Single(bars);
        Assert.Equal("2026-11", onlyBar.GetAttribute("data-bar"));

        // Advancing to Dec 2026 re-enables previous button, and returning to Nov 2026 disables it again
        await cut.Find("[data-testid='month-next']").ClickAsync();
        Assert.Contains("Dezembro de 2026", cut.Find("[data-testid='month-label']").TextContent);
        Assert.False(cut.Find("[data-testid='month-prev']").HasAttribute("disabled"));

        await cut.Find("[data-testid='month-prev']").ClickAsync();
        Assert.Contains("Novembro de 2026", cut.Find("[data-testid='month-label']").TextContent);
        Assert.True(cut.Find("[data-testid='month-prev']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task MonthlyResultChart_HoverShowsEdgeClampedTooltipAndClearsOnMouseLeave()
    {
        Clock.Today = new DateOnly(2026, 11, 24);
        var accountId = await SeedAccountAsync(initialBalance: 1000m, initialBalanceDate: new DateOnly(2026, 6, 1));
        await Executor.ExecuteAsync<RegisterIncomeHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterIncomeCommand(accountId, new DateOnly(2026, 11, 5), 2000m, "Salário"), ct),
            showErrorSnackbar: false);

        var cut = Render<DashboardPage>();
        Assert.Empty(cut.FindAll("[data-testid='monthly-result-tooltip']"));

        // First month aligns left (NoActivity shows clean empty text)
        await cut.Find("[data-testid='chart-hit-2026-06']").MouseEnterAsync();
        var firstTooltip = cut.Find("[data-testid='monthly-result-tooltip']");
        Assert.Contains("is-align-left", firstTooltip.ClassList);
        Assert.Contains("Sem movimentações registradas", firstTooltip.TextContent);

        // Last month (Nov) aligns right and displays isolated result
        await cut.Find("[data-testid='chart-hit-2026-11']").MouseEnterAsync();
        var lastTooltip = cut.Find("[data-testid='monthly-result-tooltip']");
        Assert.Contains("is-align-right", lastTooltip.ClassList);
        Assert.Contains("2.000,00", lastTooltip.TextContent);

        // Leaving stage clears tooltip cleanly
        await cut.Find(".hearth-result-chart-stage").MouseLeaveAsync();
        Assert.Empty(cut.FindAll("[data-testid='monthly-result-tooltip']"));
    }

    [Fact]
    public void MonthlyResultChart_UsesNiceNumberAxisTicksIncludingZeroAndCompactKFormatting()
    {
        var points = new List<DashboardMonthlyResultPointDto>
        {
            new(YearMonth.Of(2026, 9), 15000m, 2500m, 12500m, BreakEvenStatus.Healthy, false),
            new(YearMonth.Of(2026, 10), 1000m, 2400m, -1400m, BreakEvenStatus.Deficit, false),
            new(YearMonth.Of(2026, 11), 0m, 0m, 0m, BreakEvenStatus.NoActivity, true),
        };

        var cut = Render<MonthlyResultChart>(parameters => parameters
            .Add(p => p.Points, points)
            .Add(p => p.SelectedMonth, YearMonth.Of(2026, 11)));

        var yLabels = cut.FindAll(".hearth-chart-yaxis-label").Select(el => el.TextContent.Trim()).ToList();
        Assert.Contains("R$ 0", yLabels);
        Assert.Contains(yLabels, label => label.EndsWith("k", StringComparison.Ordinal));

        // Empty month renders neutral .is-empty bar, not orange .is-warning
        var novBar = cut.Find("[data-bar='2026-11']");
        Assert.Contains("is-empty", novBar.ClassList);
        Assert.DoesNotContain("is-warning", novBar.ClassList);
    }

    [Fact]
    public void MonthlyResultChart_AppliesSemanticClassesForAllFourBreakEvenStatesAndOmitsDuplicateBadge()
    {
        var points = new List<DashboardMonthlyResultPointDto>
        {
            new(YearMonth.Of(2026, 8), 3000m, 1000m, 2000m, BreakEvenStatus.Healthy, false),
            new(YearMonth.Of(2026, 9), 1000m, 950m, 50m, BreakEvenStatus.BreakEvenWarning, false),
            new(YearMonth.Of(2026, 10), 1000m, 1400m, -400m, BreakEvenStatus.Deficit, false),
            new(YearMonth.Of(2026, 11), 0m, 0m, 0m, BreakEvenStatus.NoActivity, true),
        };

        var cut = Render<MonthlyResultChart>(parameters => parameters
            .Add(p => p.Points, points)
            .Add(p => p.SelectedMonth, YearMonth.Of(2026, 11)));

        Assert.Empty(cut.FindAll(".hearth-breakeven-badge"));
        Assert.Contains("is-healthy", cut.Find("[data-bar='2026-08']").ClassList);
        Assert.Contains("is-warning", cut.Find("[data-bar='2026-09']").ClassList);
        Assert.Contains("is-deficit", cut.Find("[data-bar='2026-10']").ClassList);
        Assert.Contains("is-empty", cut.Find("[data-bar='2026-11']").ClassList);
        Assert.DoesNotContain("is-warning", cut.Find("[data-bar='2026-11']").ClassList);
    }

    [Fact]
    public void MonthlyResultChart_WhenPointsEmpty_ShowsEmptyState()
    {
        var cut = Render<MonthlyResultChart>(parameters => parameters
            .Add(p => p.Points, [])
            .Add(p => p.SelectedMonth, YearMonth.Of(2026, 11)));

        Assert.NotEmpty(cut.FindAll("[data-testid='monthly-result-empty-state']"));
        Assert.Empty(cut.FindAll(".hearth-result-chart-svg"));
    }

    [Fact]
    public void SavingsRateRing_DistinguishesDeficitBreakEvenAndBandColors()
    {
        // Deficit: red pill "Déficit", red full ring, red 0,0% and explicit deficit alert
        var deficit = Render<SavingsRateRing>(parameters => parameters
            .Add(p => p.Percentage, 0m)
            .Add(p => p.HasActivity, true)
            .Add(p => p.MonthlyResult, -450.25m));

        Assert.Equal("Déficit", deficit.Find("[data-testid='savings-rate-status-cue']").TextContent.Trim());
        Assert.Contains("is-deficit", deficit.Find("[data-testid='savings-rate-status-cue']").ClassList);
        Assert.Contains("is-deficit", deficit.Find("[data-testid='savings-rate-value']").ClassList);
        Assert.Contains("is-deficit", deficit.Find(".hearth-savings-ring-progress").ClassList);
        Assert.Contains("450,25", deficit.Find("[data-testid='savings-rate-alert']").TextContent);

        // Break-even: "No limite", dashed zero-alert ring, attention alert
        var breakEven = Render<SavingsRateRing>(parameters => parameters
            .Add(p => p.Percentage, 0m)
            .Add(p => p.HasActivity, true)
            .Add(p => p.MonthlyResult, 0m));

        Assert.Equal("No limite", breakEven.Find("[data-testid='savings-rate-status-cue']").TextContent.Trim());
        Assert.Contains("is-zero-alert", breakEven.Find(".hearth-savings-ring-progress").ClassList);
        Assert.Contains("Empate", breakEven.Find("[data-testid='savings-rate-alert']").TextContent);

        // Band colors: <10% attention, 10-20% balanced, >=20% healthy
        var attention = Render<SavingsRateRing>(parameters => parameters
            .Add(p => p.Percentage, 6.5m)
            .Add(p => p.HasActivity, true)
            .Add(p => p.MonthlyResult, 130m));
        Assert.Contains("is-attention", attention.Find(".hearth-savings-ring-progress").ClassList);
        Assert.Empty(attention.FindAll("[data-testid='savings-rate-alert']"));

        var balanced = Render<SavingsRateRing>(parameters => parameters
            .Add(p => p.Percentage, 15.0m)
            .Add(p => p.HasActivity, true)
            .Add(p => p.MonthlyResult, 300m));
        Assert.Contains("is-balanced", balanced.Find(".hearth-savings-ring-progress").ClassList);

        var healthy = Render<SavingsRateRing>(parameters => parameters
            .Add(p => p.Percentage, 28.0m)
            .Add(p => p.HasActivity, true)
            .Add(p => p.MonthlyResult, 560m));
        Assert.Contains("is-healthy", healthy.Find(".hearth-savings-ring-progress").ClassList);
    }

    [Fact]
    public async Task Dashboard_NextMonthBalanceProjection_ShowsFilterNoteUnderConditionFilter()
    {
        Clock.Today = new DateOnly(2026, 11, 24);
        var accountId = await SeedAccountAsync(initialBalance: 1500m, initialBalanceDate: new DateOnly(2026, 10, 1));
        await Executor.ExecuteAsync<RegisterIncomeHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterIncomeCommand(accountId, new DateOnly(2026, 12, 5), 2000m, "Salário Dez"), ct),
            showErrorSnackbar: false);

        var cut = Render<DashboardPage>();
        Assert.Contains("1.500,00", cut.Find("[data-testid='next-month-opening-balance']").TextContent);
        Assert.Contains("3.500,00", cut.Find("[data-testid='next-month-ending-balance']").TextContent);

        await cut.Find("[data-testid='dashboard-condition-own']").ClickAsync();
        Assert.Empty(cut.FindAll("[data-testid='next-month-balance-projection']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='next-month-balance-filter-note']"));
    }
}
