using Bunit;
using Hearth.Application.CreditCardPurchases.RegisterCreditCardPurchase;
using Hearth.Application.CreditCards.ArchiveCreditCard;
using Hearth.Application.Invoices.GetInvoiceSummary;
using Hearth.Application.PurchaseConditions;
using Hearth.Application.PurchaseConditions.CreatePurchaseCondition;
using Hearth.Domain;
using Hearth.Web.Components.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Extensions;

namespace Hearth.Tests.Web.Pages;

public sealed class CardPagesTests : WebTestBase
{
    [Fact]
    public async Task CardPage_WhenNoCardExists_AllowsCreatingActiveCardAndShowsVisualCardWithLimits()
    {
        await SeedAccountAsync();
        Render<MudPopoverProvider>();

        var cut = Render<CardPage>();

        Assert.NotEmpty(cut.FindAll("[data-testid='no-active-card-panel']"));

        cut.Find("[data-testid='card-name-input']").Input("Nubank Ultravioleta");
        cut.Find("[data-testid='card-limit-input']").Input("6000");
        cut.Find("[data-testid='card-closing-day-input']").Input("3");
        cut.Find("[data-testid='card-due-day-input']").Input("12");

        await cut.Find("[data-testid='save-card-submit']").ClickAsync();

        Assert.Contains("Nubank Ultravioleta", cut.Find("[data-testid='active-card-name']").TextContent);
        Assert.Contains("6.000,00", cut.Find("[data-testid='card-available-limit']").TextContent);
        Assert.Contains("6.000,00", cut.Find("[data-testid='card-total-limit']").TextContent);
        Assert.NotEmpty(cut.FindAll("[data-testid='delete-card-button']"));
    }

    [Fact]
    public async Task CardPage_CardWithoutPurchases_CanBeDeleted()
    {
        var accountId = await SeedAccountAsync();
        await SeedCreditCardAsync(accountId, closingDay: 3, dueDay: 12, name: "Cartão Sem Uso", limit: 3000m);
        Render<MudPopoverProvider>();

        var cut = Render<CardPage>();

        await cut.Find("[data-testid='delete-card-button']").ClickAsync();
        await cut.Find("[data-testid='confirm-delete-card-button']").ClickAsync();

        Assert.NotEmpty(cut.FindAll("[data-testid='no-active-card-panel']"));
    }

    [Fact]
    public async Task CardPage_CardWithPurchases_OffersArchiveAndPreservesHistoricalPurchases()
    {
        var accountId = await SeedAccountAsync();
        var cardId = await SeedCreditCardAsync(accountId, closingDay: 3, dueDay: 12, name: "Cartão Antigo", limit: 4000m);

        await Executor.ExecuteAsync<RegisterCreditCardPurchaseHandler, Guid>(
            (handler, ct) => handler.ExecuteAsync(
                new RegisterCreditCardPurchaseCommand(
                    accountId,
                    cardId,
                    new DateOnly(2026, 11, 10),
                    500m,
                    1,
                    1,
                    SystemPurchaseConditions.OwnId,
                    "Fone Bluetooth",
                    SystemCategories.ShoppingId),
                ct),
            showErrorSnackbar: false);

        Render<MudPopoverProvider>();
        var cut = Render<CardPage>();

        Assert.Empty(cut.FindAll("[data-testid='delete-card-button']"));
        await cut.Find("[data-testid='archive-card-button']").ClickAsync();
        await cut.Find("[data-testid='confirm-archive-card-button']").ClickAsync();

        Assert.NotEmpty(cut.FindAll("[data-testid='no-active-card-panel']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='archived-cards-section']"));
        await cut.Find("[data-testid='card-tab-purchases']").ClickAsync();
        Assert.Contains("Fone Bluetooth", cut.Find("[data-testid='card-purchases-panel'] tbody").TextContent);
    }

    [Fact]
    public async Task CardPage_DefaultsToOpenInvoiceCycleAndClickingPhysicalCardReturnsToInvoiceTab()
    {
        var accountId = await SeedAccountAsync();
        var cardId = await SeedCreditCardAsync(accountId, closingDay: 3, dueDay: 12);

        // Today is Nov 20, closing day is 3 -> open cycle is Dec 2026
        await Executor.ExecuteAsync<RegisterCreditCardPurchaseHandler, Guid>(
            (handler, ct) => handler.ExecuteAsync(
                new RegisterCreditCardPurchaseCommand(
                    accountId,
                    cardId,
                    new DateOnly(2026, 11, 10),
                    1000m,
                    1,
                    1,
                    SystemPurchaseConditions.OwnId,
                    "Notebook",
                    SystemCategories.ShoppingId),
                ct),
            showErrorSnackbar: false);

        Render<MudPopoverProvider>();
        var cut = Render<CardPage>();

        Assert.Equal("true", cut.Find("[data-testid='card-tab-invoice']").GetAttribute("aria-selected"));
        Assert.Contains("Dezembro de 2026", cut.Find("[data-testid='month-label']").TextContent);
        Assert.Contains("1.000,00", cut.Find("[data-testid='invoice-calculated-amount']").TextContent);
        Assert.Contains("Em aberto", cut.Find("[data-testid='invoice-status-chip']").TextContent);

        // Switch to Purchases tab, then click visual card to jump back to Fatura
        await cut.Find("[data-testid='card-tab-purchases']").ClickAsync();
        Assert.Equal("true", cut.Find("[data-testid='card-tab-purchases']").GetAttribute("aria-selected"));

        await cut.Find("[data-testid='physical-card-button']").ClickAsync();
        Assert.Equal("true", cut.Find("[data-testid='card-tab-invoice']").GetAttribute("aria-selected"));
    }

    [Fact]
    public async Task CardPage_LegacyComprasRoute_OpensPurchasesTabAndSupportsValidationAndCreation()
    {
        var accountId = await SeedAccountAsync();
        var cardId = await SeedCreditCardAsync(accountId, closingDay: 3, dueDay: 12);
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/cartao/compras");

        Render<MudPopoverProvider>();
        var cut = Render<CardPage>();

        Assert.Equal("true", cut.Find("[data-testid='card-tab-purchases']").GetAttribute("aria-selected"));

        // Open form and click submit without filling fields -> button is NOT pre-disabled and shows inline errors
        await cut.Find("[data-testid='new-purchase-button']").ClickAsync();
        var submitButton = cut.Find("[data-testid='create-purchase-submit']");
        Assert.False(submitButton.HasAttribute("disabled"));

        await submitButton.ClickAsync();
        Assert.NotEmpty(cut.FindAll("[data-testid='create-purchase-errors']"));

        // Fill valid fields and register
        cut.Find("[data-testid='purchase-description-input']").Input("Teclado Mecânico");
        cut.Find("[data-testid='purchase-amount-input']").Input("450");
        await cut.Find("[data-testid='create-purchase-submit']").ClickAsync();

        Assert.Contains("Teclado Mecânico", cut.Find("tbody").TextContent);
        Assert.Contains("450,00", cut.Find("[data-testid='card-used-limit']").TextContent);
    }

    [Fact]
    public async Task CardPage_DynamicCustomChipsFilterAndLockedPurchaseDisablesAmountAndDate()
    {
        var accountId = await SeedAccountAsync();
        var cardId = await SeedCreditCardAsync(accountId, closingDay: 3, dueDay: 12);
        var categoryId = SystemCategories.HealthId;

        var momCondition = await Executor.ExecuteAsync<CreatePurchaseConditionHandler, PurchaseConditionDto>(
            (handler, ct) => handler.ExecuteAsync(new CreatePurchaseConditionCommand("Mãe"), ct),
            showErrorSnackbar: false);

        // Locked purchase (installment 2 due Nov 12; today is Nov 20 -> Paid -> IsLocked == true)
        Clock.Today = new DateOnly(2026, 11, 1);
        var lockedResult = await Executor.ExecuteAsync<RegisterCreditCardPurchaseHandler, Guid>(
            (handler, ct) => handler.ExecuteAsync(
                new RegisterCreditCardPurchaseCommand(
                    accountId,
                    cardId,
                    new DateOnly(2026, 9, 15),
                    300m,
                    3,
                    2,
                    momCondition.Value!.Id,
                    "Farmácia Mãe",
                    categoryId),
                ct),
            showErrorSnackbar: false);
        Clock.Today = new DateOnly(2026, 11, 20);

        // Open purchase (Nov 10 -> closes Dec 3, due Dec 12; today is Nov 20 -> unlocked)
        var openResult = await Executor.ExecuteAsync<RegisterCreditCardPurchaseHandler, Guid>(
            (handler, ct) => handler.ExecuteAsync(
                new RegisterCreditCardPurchaseCommand(
                    accountId,
                    cardId,
                    new DateOnly(2026, 11, 10),
                    150m,
                    1,
                    1,
                    SystemPurchaseConditions.OwnId,
                    "Livros",
                    categoryId),
                ct),
            showErrorSnackbar: false);

        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/cartao?aba=compras");

        Render<MudPopoverProvider>();
        var cut = Render<CardPage>();

        // Dynamic Custom chip "Mãe" exists and filters table
        await cut.Find("[data-testid='filter-condition-custom-Mãe']").ClickAsync();
        var body = cut.Find("tbody").TextContent;
        Assert.Contains("Farmácia Mãe", body);
        Assert.DoesNotContain("Livros", body);

        // Redundant quick-edit amount/date buttons are removed from row actions; only expand, single Edit (pencil) and Delete exist
        Assert.Empty(cut.FindAll($"[data-testid='edit-amount-purchase-{lockedResult.Value}']"));
        Assert.Empty(cut.FindAll($"[data-testid='edit-date-purchase-{lockedResult.Value}']"));

        var lockedEditBtn = cut.Find($"[data-testid='edit-purchase-{lockedResult.Value}']");
        var deleteBtn = cut.Find($"[data-testid='delete-purchase-{lockedResult.Value}']");
        Assert.False(lockedEditBtn.HasAttribute("disabled"));
        Assert.True(deleteBtn.HasAttribute("disabled"));
        Assert.Contains("fatura já paga", deleteBtn.GetAttribute("title"));
        Assert.Contains("Fatura paga", cut.Find($"[data-testid='purchase-status-{lockedResult.Value}']").TextContent);

        // Opening unified Edit panel on locked purchase keeps description/category/condition editable while locking amount & date
        await lockedEditBtn.ClickAsync();
        Assert.NotEmpty(cut.FindAll("[data-testid='edit-purchase-locked-notice']"));
        Assert.False(cut.Find("[data-testid='edit-purchase-description-input']").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid='edit-purchase-amount-input']").HasAttribute("disabled"));
        Assert.True(cut.Find("input[data-testid='edit-purchase-date-input']").HasAttribute("disabled"));

        // Expand installments shows "Fatura paga" for tracked paid installments (Oct 12 and Nov 12 >= TrackingStartDate Jan 1)
        await cut.Find($"[data-testid='expand-purchase-{lockedResult.Value}']").ClickAsync();
        Assert.Contains("Fatura paga", cut.Markup);

        // Open purchase keeps unified edit (including amount & date) and delete enabled, and saves changes via the single Edit flow
        await cut.Find("[data-testid='filter-condition-all']").ClickAsync();
        Assert.False(cut.Find($"[data-testid='delete-purchase-{openResult.Value}']").HasAttribute("disabled"));
        await cut.Find($"[data-testid='edit-purchase-{openResult.Value}']").ClickAsync();
        Assert.Empty(cut.FindAll("[data-testid='edit-purchase-locked-notice']"));
        Assert.False(cut.Find("[data-testid='edit-purchase-amount-input']").HasAttribute("disabled"));
        Assert.False(cut.Find("input[data-testid='edit-purchase-date-input']").HasAttribute("disabled"));

        cut.Find("[data-testid='edit-purchase-description-input']").Input("Livros Técnicos");
        cut.Find("[data-testid='edit-purchase-amount-input']").Input("220");
        await cut.Find("[data-testid='save-purchase-edit']").ClickAsync();

        var updatedTable = cut.Find("[data-testid='card-purchases-panel'] tbody").TextContent;
        Assert.Contains("Livros Técnicos", updatedTable);
        Assert.Contains("220,00", updatedTable);
    }

    [Fact]
    public async Task CardPage_ClosedButUnpaidInvoicePurchase_RemainsEditableAndDeletableUntilDueDate()
    {
        // Rule (3, 12): purchase on Oct 10 closes Nov 3 and is due Nov 12. On Nov 10 it is Closed but NOT paid!
        Clock.Today = new DateOnly(2026, 11, 10);
        var accountId = await SeedAccountAsync();
        var cardId = await SeedCreditCardAsync(accountId, closingDay: 3, dueDay: 12);

        var purchaseResult = await Executor.ExecuteAsync<RegisterCreditCardPurchaseHandler, Guid>(
            (handler, ct) => handler.ExecuteAsync(
                new RegisterCreditCardPurchaseCommand(
                    accountId,
                    cardId,
                    new DateOnly(2026, 10, 10),
                    200m,
                    1,
                    1,
                    SystemPurchaseConditions.OwnId,
                    "Compra Fechada Não Paga",
                    SystemCategories.ShoppingId),
                ct),
            showErrorSnackbar: false);

        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/cartao?aba=compras");

        Render<MudPopoverProvider>();
        var cut = Render<CardPage>();

        Assert.False(cut.Find($"[data-testid='delete-purchase-{purchaseResult.Value}']").HasAttribute("disabled"));
        await cut.Find($"[data-testid='edit-purchase-{purchaseResult.Value}']").ClickAsync();
        Assert.Empty(cut.FindAll("[data-testid='edit-purchase-locked-notice']"));
        Assert.False(cut.Find("[data-testid='edit-purchase-amount-input']").HasAttribute("disabled"));
        Assert.False(cut.Find("input[data-testid='edit-purchase-date-input']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task CardPage_ArchivedCards_ExcludedFromPurchaseSelector_CanBeReactivatedAndSafelyDeleted()
    {
        var accountId = await SeedAccountAsync();
        var archivedWithHistoryId = await SeedCreditCardAsync(accountId, closingDay: 3, dueDay: 12, name: "Cartão Histórico", limit: 4000m);

        await Executor.ExecuteAsync<RegisterCreditCardPurchaseHandler, Guid>(
            (handler, ct) => handler.ExecuteAsync(
                new RegisterCreditCardPurchaseCommand(
                    accountId,
                    archivedWithHistoryId,
                    new DateOnly(2026, 11, 10),
                    300m,
                    1,
                    1,
                    SystemPurchaseConditions.OwnId,
                    "Compra Histórica",
                    SystemCategories.ShoppingId),
                ct),
            showErrorSnackbar: false);

        await Executor.ExecuteAsync<ArchiveCreditCardHandler, bool>(
            (handler, ct) => handler.ExecuteAsync(new ArchiveCreditCardCommand(archivedWithHistoryId), ct),
            showErrorSnackbar: false);

        var activeCardId = await SeedCreditCardAsync(accountId, closingDay: 10, dueDay: 20, name: "Cartão Ativo Atual", limit: 6000m);

        Render<MudPopoverProvider>();
        var cut = Render<CardPage>();

        // 1. Archived card section sits discreetly collapsed when an active card exists; expanding reveals history block & disabled restore
        var archivedToggle = cut.Find("[data-testid='archived-cards-toggle']");
        Assert.Equal("false", archivedToggle.GetAttribute("aria-expanded"));
        await archivedToggle.ClickAsync();

        var archivedSection = cut.Find("[data-testid='archived-cards-section']");
        Assert.Contains("Cartão Histórico", archivedSection.TextContent);
        Assert.NotEmpty(cut.FindAll($"[data-testid='archived-card-delete-blocked-{archivedWithHistoryId}']"));
        Assert.Empty(cut.FindAll($"[data-testid='delete-archived-card-{archivedWithHistoryId}']"));
        Assert.True(cut.Find($"[data-testid='restore-card-{archivedWithHistoryId}']").HasAttribute("disabled"));

        // 2. Opening New Purchase form shows ONLY active card in card selector (archived card excluded)
        await cut.Find("[data-testid='new-purchase-button']").ClickAsync();
        var cardSelect = cut.FindComponents<MudSelect<Guid>>().Single(s => s.Instance.Label == "Cartão");
        Assert.Equal(activeCardId, cardSelect.Instance.GetState(x => x.Value));
        var cardItems = cardSelect.FindComponents<MudSelectItem<Guid>>();
        Assert.Single(cardItems);
        Assert.Equal(activeCardId, cardItems[0].Instance.Value);

        // 3. Archive active unused card -> delete archived unused card -> Restore button on archived card with history becomes enabled -> Reactivate!
        await cut.Find("[data-testid='archive-card-button']").ClickAsync();
        await cut.Find("[data-testid='confirm-archive-card-button']").ClickAsync();

        await cut.Find($"[data-testid='delete-archived-card-{activeCardId}']").ClickAsync();
        await cut.Find($"[data-testid='confirm-delete-archived-card-{activeCardId}']").ClickAsync();
        Assert.Empty(cut.FindAll($"[data-testid='restore-card-{activeCardId}']"));

        var restoreBtn = cut.Find($"[data-testid='restore-card-{archivedWithHistoryId}']");
        Assert.False(restoreBtn.HasAttribute("disabled"));
        await restoreBtn.ClickAsync();

        Assert.Contains("Cartão Histórico", cut.Find("[data-testid='active-card-name']").TextContent);
        Assert.Empty(cut.FindAll("[data-testid='archived-cards-section']"));
    }

    [Fact]
    public async Task CardPage_CrossMonthRule_AlignsInvoicesByDueDateIdenticallyToDashboard()
    {
        // Rule (25, 5), today Nov 20: purchase on Nov 10 closes Nov 25 and is due Dec 5 -> DueMonth = Dec 2026
        Clock.Today = new DateOnly(2026, 11, 20);
        var accountId = await SeedAccountAsync();
        var cardId = await SeedCreditCardAsync(accountId, closingDay: 25, dueDay: 5, name: "Cartão 25/5", limit: 5000m);

        await Executor.ExecuteAsync<RegisterCreditCardPurchaseHandler, Guid>(
            (handler, ct) => handler.ExecuteAsync(
                new RegisterCreditCardPurchaseCommand(
                    accountId,
                    cardId,
                    new DateOnly(2026, 11, 10),
                    750m,
                    1,
                    1,
                    SystemPurchaseConditions.OwnId,
                    "Passagem",
                    SystemCategories.TransportId),
                ct),
            showErrorSnackbar: false);

        Render<MudPopoverProvider>();
        var cut = Render<CardPage>();

        // Default open invoice on /cartao must be Dec 2026 (DueMonth), matching Dashboard's DueMonth summary
        Assert.Contains("Dezembro de 2026", cut.Find("[data-testid='month-label']").TextContent);
        Assert.Contains("750,00", cut.Find("[data-testid='invoice-calculated-amount']").TextContent);

        var dashboardInvoice = await Executor.ExecuteAsync<GetInvoiceSummaryHandler, InvoiceSummaryDto>(
            (handler, ct) => handler.ExecuteAsync(
                new GetInvoiceSummaryQuery(accountId, 2026, 12, MonthBasis: InvoiceMonthBasis.DueMonth), ct),
            showErrorSnackbar: false);
        Assert.Equal(750m, dashboardInvoice.Value!.CalculatedAmount);
    }

    [Fact]
    public async Task CardPage_LegacyFaturasRoute_DisplaysDerivedCalculatedAmountAndAutomaticStatus()
    {
        var accountId = await SeedAccountAsync();
        var cardId = await SeedCreditCardAsync(accountId, closingDay: 3, dueDay: 12);
        var categoryId = SystemCategories.ShoppingId;

        // Purchase registered on Oct 25 -> closes Nov 3, due Nov 12 (later viewed on Nov 20 -> Paid)
        Clock.Today = new DateOnly(2026, 10, 25);
        await Executor.ExecuteAsync<RegisterCreditCardPurchaseHandler, Guid>(
            (handler, ct) => handler.ExecuteAsync(
                new RegisterCreditCardPurchaseCommand(
                    accountId,
                    cardId,
                    new DateOnly(2026, 10, 20),
                    1000m,
                    1,
                    1,
                    SystemPurchaseConditions.OwnId,
                    "Notebook",
                    categoryId),
                ct),
            showErrorSnackbar: false);
        Clock.Today = new DateOnly(2026, 11, 20);

        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/cartao/faturas");

        Render<MudPopoverProvider>();
        var cut = Render<CardPage>();

        // Default opens on Dec 2026 (open cycle); navigate back 1 month to Nov 2026 (Paid)
        await cut.Find("[data-testid='month-prev']").ClickAsync();

        Assert.Contains("1.000,00", cut.Find("[data-testid='invoice-calculated-amount']").TextContent);
        Assert.Contains("Paga", cut.Find("[data-testid='invoice-status-chip']").TextContent);
    }

    [Fact]
    public async Task CardPage_PurchaseExceedingLimit_ShowsSoftWarningAllowsSubmitAndHighlightsExceededLimit()
    {
        var accountId = await SeedAccountAsync();
        await SeedCreditCardAsync(accountId, closingDay: 3, dueDay: 12, name: "Cartão BB", limit: 600m);

        Render<MudPopoverProvider>();
        var cut = Render<CardPage>();

        await cut.Find("[data-testid='new-purchase-button']").ClickAsync();
        cut.Find("[data-testid='purchase-description-input']").Input("Monitor");
        cut.Find("[data-testid='purchase-amount-input']").Input("799");

        // Soft warning appears in real time with exact excess (199,00) without blocking submit
        var warning = cut.Find("[data-testid='create-purchase-limit-warning']");
        Assert.Contains("199,00", warning.TextContent);
        Assert.Contains("600,00", warning.TextContent);
        Assert.False(cut.Find("[data-testid='create-purchase-submit']").HasAttribute("disabled"));

        // Submitting still succeeds and overview highlights exceeded limit
        await cut.Find("[data-testid='create-purchase-submit']").ClickAsync();

        Assert.Contains("Monitor", cut.Find("tbody").TextContent);
        Assert.Contains("Limite excedido", cut.Find("[data-testid='card-limit-status-pill']").TextContent);
        Assert.Contains("199,00", cut.Find("[data-testid='card-limit-exceeded-note']").TextContent);
    }

    [Fact]
    public async Task CardPage_AllowsRegisteringPurchaseBeforeInitialBalanceDateWhenDueDateIsOpen()
    {
        Clock.Today = new DateOnly(2026, 11, 23);
        var accountId = await SeedAccountAsync(initialBalance: 2000m, initialBalanceDate: new DateOnly(2026, 11, 23));
        await SeedCreditCardAsync(accountId, closingDay: 25, dueDay: 5, name: "Cartão Principal", limit: 3000m);

        Render<MudPopoverProvider>();
        var cut = Render<CardPage>();

        await cut.Find("[data-testid='new-purchase-button']").ClickAsync();
        Assert.Contains("Resto de centavos vai na primeira parcela.", cut.Find("[data-testid='create-purchase-form']").TextContent);
        cut.Find("[data-testid='purchase-description-input']").Input("Compra pré-saldo");
        cut.Find("[data-testid='purchase-amount-input']").Input("350");
        cut.Find("input[data-testid='purchase-date-input']").Change("08/11/2026");

        await cut.Find("[data-testid='create-purchase-submit']").ClickAsync();

        Assert.Contains("Compra pré-saldo", cut.Find("tbody").TextContent);
        Assert.Contains("350,00", cut.Find("[data-testid='card-used-limit']").TextContent);
        var saved = Assert.Single(await CreditCardPurchases.GetByAccountAsync(accountId));
        Assert.Equal(new DateOnly(2026, 11, 8), saved.Date);
        Assert.Equal(new DateOnly(2026, 12, 5), saved.FirstActiveCycle.DueDate);
    }

    [Fact]
    public async Task CardPage_AllowsCreatingNewConditionInlineAndReusingItInFuturePurchases()
    {
        var accountId = await SeedAccountAsync();
        await SeedCreditCardAsync(accountId, closingDay: 3, dueDay: 12, name: "Nubank", limit: 5000m);

        Render<MudPopoverProvider>();
        var cut = Render<CardPage>();

        // 1. Open New Purchase form -> "Própria" is selected by default and "Outros" is available as global default
        await cut.Find("[data-testid='new-purchase-button']").ClickAsync();
        var conditionSelect = cut.FindComponents<MudSelect<Guid>>().Single(s => s.Instance.Label == "Condição");
        Assert.Equal(SystemPurchaseConditions.OwnId, conditionSelect.Instance.GetState(x => x.Value));
        var initialConditions = await PurchaseConditions.GetVisibleForUserAsync(CurrentUser.UserId!.Value);
        Assert.Contains(initialConditions, c => c.Id == SystemPurchaseConditions.OthersId && c.Name == "Outros");

        // 2. Select "+ Nova condição" (Guid.Empty), enter "Viagem", save inline
        await cut.InvokeAsync(() => conditionSelect.Instance.ValueChanged.InvokeAsync(Guid.Empty));
        cut.Find("[data-testid='purchase-condition-name-input']").Input("Viagem");
        await cut.Find("[data-testid='save-new-condition-button']").ClickAsync();

        var savedConditions = await PurchaseConditions.GetVisibleForUserAsync(CurrentUser.UserId!.Value);
        var viagem = Assert.Single(savedConditions, c => c.Name == "Viagem");
        Assert.Equal(viagem.Id, conditionSelect.Instance.GetState(x => x.Value));

        // 3. Submit first purchase using the newly created condition
        cut.Find("[data-testid='purchase-description-input']").Input("Hotel");
        cut.Find("[data-testid='purchase-amount-input']").Input("500");
        await cut.Find("[data-testid='create-purchase-submit']").ClickAsync();

        Assert.Contains("Hotel", cut.Find("tbody").TextContent);
        Assert.Contains("Viagem", cut.Find("tbody").TextContent);

        // 4. Open New Purchase form again -> "Viagem" is already available for reuse without re-creating
        await cut.Find("[data-testid='new-purchase-button']").ClickAsync();
        var secondConditionSelect = cut.FindComponents<MudSelect<Guid>>().Single(s => s.Instance.Label == "Condição");
        Assert.Equal(SystemPurchaseConditions.OwnId, secondConditionSelect.Instance.GetState(x => x.Value));

        await cut.InvokeAsync(() => secondConditionSelect.Instance.ValueChanged.InvokeAsync(viagem.Id));
        cut.Find("[data-testid='purchase-description-input']").Input("Passagem");
        cut.Find("[data-testid='purchase-amount-input']").Input("800");
        await cut.Find("[data-testid='create-purchase-submit']").ClickAsync();

        var purchases = await CreditCardPurchases.GetByAccountAsync(accountId);
        Assert.Equal(2, purchases.Count);
        Assert.All(purchases, p => Assert.Equal(viagem.Id, p.ConditionId));

        // Still only one "Viagem" condition in repository
        var allConditionsAfter = await PurchaseConditions.GetVisibleForUserAsync(CurrentUser.UserId!.Value);
        Assert.Single(allConditionsAfter, c => c.Name == "Viagem");
    }

    [Fact]
    public async Task CardPage_OngoingPurchaseWithInitialInstallmentSkippingEntryMonth_ShowsAndSumsEntryMonthPaidInvoice()
    {
        // Account created Nov 25, 2026 -> TrackingStartDate = 2026-11-01; card closes day 2, due day 14
        Clock.Today = new DateOnly(2026, 11, 25);
        var accountId = await SeedAccountAsync(initialBalanceDate: new DateOnly(2026, 11, 25));
        var cardId = await SeedCreditCardAsync(accountId, closingDay: 2, dueDay: 14);

        var purchaseResult = await Executor.ExecuteAsync<RegisterCreditCardPurchaseHandler, Guid>(
            (handler, ct) => handler.ExecuteAsync(
                new RegisterCreditCardPurchaseCommand(
                    accountId,
                    cardId,
                    new DateOnly(2026, 3, 2),
                    2400.00m,
                    10,
                    9,
                    SystemPurchaseConditions.OwnId,
                    "Celular S23 Ultra",
                    SystemCategories.ShoppingId),
                ct),
            showErrorSnackbar: false);
        Assert.True(purchaseResult.IsSuccess);

        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/cartao?aba=fatura");

        Render<MudPopoverProvider>();
        var cut = Render<CardPage>();

        // Default cycle is Dec 2026 (R$ 240,00); navigate back to entry month Nov 2026 (paid invoice)
        Assert.Contains("240,00", cut.Find("[data-testid='invoice-calculated-amount']").TextContent);
        await cut.Find("[data-testid='month-prev']").ClickAsync();

        // Nov 2026 invoice MUST show installment 8/10 (R$ 240,00) with status "Paga"
        Assert.Contains("240,00", cut.Find("[data-testid='invoice-calculated-amount']").TextContent);
        Assert.Contains("Paga", cut.Find("[data-testid='invoice-status-chip']").TextContent);
        Assert.Contains("Celular S23 Ultra", cut.Find("[data-testid='card-invoice-panel']").TextContent);

        // Switch to Compras tab and expand purchase -> shows "Já paga (fora do período)" for 1..7, "Fatura paga" for 8, "Na fatura" for 9..10
        await cut.Find("[data-testid='card-tab-purchases']").ClickAsync();
        await cut.Find($"[data-testid='expand-purchase-{purchaseResult.Value}']").ClickAsync();
        Assert.Contains("Já paga (fora do período)", cut.Markup);
        Assert.Contains("Fatura paga", cut.Markup);
        Assert.Contains("Na fatura", cut.Markup);
    }
}
