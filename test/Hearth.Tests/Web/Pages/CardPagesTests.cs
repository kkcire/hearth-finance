using Bunit;
using Hearth.Application.CreditCardPurchases.RegisterCreditCardPurchase;
using Hearth.Domain;
using Hearth.Domain.ValueObjects;
using Hearth.Web.Components.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

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
                    PurchaseConditionKind.Own,
                    null,
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
        await cut.Find("[data-testid='card-tab-purchases']").ClickAsync();
        Assert.Contains("Fone Bluetooth", cut.Find("tbody").TextContent);
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
                    PurchaseConditionKind.Own,
                    null,
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

        // Locked purchase (registered on Nov 1 before Nov 12 due date; on Nov 20 installment 2 closed Nov 3 -> IsLocked == true)
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
                    PurchaseConditionKind.Custom,
                    "Mãe",
                    "Farmácia Mãe",
                    categoryId),
                ct),
            showErrorSnackbar: false);
        Clock.Today = new DateOnly(2026, 11, 20);

        // Open purchase (Nov 10 -> closes Dec 3, today is Nov 20)
        var openResult = await Executor.ExecuteAsync<RegisterCreditCardPurchaseHandler, Guid>(
            (handler, ct) => handler.ExecuteAsync(
                new RegisterCreditCardPurchaseCommand(
                    accountId,
                    cardId,
                    new DateOnly(2026, 11, 10),
                    150m,
                    1,
                    1,
                    PurchaseConditionKind.Own,
                    null,
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

        // Locked purchase disables amount & date edit, keeps details edit enabled
        Assert.True(cut.Find($"[data-testid='edit-amount-purchase-{lockedResult.Value}']").HasAttribute("disabled"));
        Assert.True(cut.Find($"[data-testid='edit-date-purchase-{lockedResult.Value}']").HasAttribute("disabled"));
        Assert.False(cut.Find($"[data-testid='edit-details-purchase-{lockedResult.Value}']").HasAttribute("disabled"));

        // Expand installments shows "Já paga" for installment 1/3
        await cut.Find($"[data-testid='expand-purchase-{lockedResult.Value}']").ClickAsync();
        Assert.Contains("Já paga", cut.Markup);

        // Open purchase keeps amount & date edit enabled
        await cut.Find("[data-testid='filter-condition-all']").ClickAsync();
        Assert.False(cut.Find($"[data-testid='edit-amount-purchase-{openResult.Value}']").HasAttribute("disabled"));
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
                    PurchaseConditionKind.Own,
                    null,
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
}
