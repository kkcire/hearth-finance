using Bunit;
using Hearth.Application.CreditCardPurchases.RegisterCreditCardPurchase;
using Hearth.Domain;
using Hearth.Domain.ValueObjects;
using Hearth.Web.Components.Pages;
using MudBlazor;

namespace Hearth.Tests.Web.Pages;

public sealed class CardPagesTests : WebTestBase
{
    [Fact]
    public async Task CreditCardPurchasesPage_DynamicCustomChipsFilterAndLockedPurchaseDisablesAmountAndDate()
    {
        var accountId = await SeedAccountAsync();
        var cardId = await SeedCreditCardAsync(accountId, closingDay: 3, dueDay: 12);
        var categoryId = SystemCategories.HealthId;

        // Locked purchase (Sep 15 with InitialInstallment=2: installment 1 closes Oct 3 [paid],
        // first active installment 2 closes Nov 3 <= today Nov 20 -> IsLocked == true)
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

        Render<MudPopoverProvider>();
        var cut = Render<CreditCardPurchasesPage>();

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
    public async Task InvoicesPage_DisplaysDerivedCalculatedAmountAndAutomaticStatus()
    {
        var accountId = await SeedAccountAsync();
        var cardId = await SeedCreditCardAsync(accountId, closingDay: 3, dueDay: 12);
        var categoryId = SystemCategories.ShoppingId;

        // Purchase on Oct 20 -> closes Nov 3, due Nov 12 (today is Nov 20 -> Paid)
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

        var cut = Render<InvoicesPage>();

        Assert.Contains("1.000,00", cut.Find("[data-testid='invoice-calculated-amount']").TextContent);
        Assert.Contains("Paga", cut.Find("[data-testid='invoice-status-chip']").TextContent);
    }
}
