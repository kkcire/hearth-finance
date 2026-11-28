using Bunit;
using Hearth.Application.CreditCardPurchases.RegisterCreditCardPurchase;
using Hearth.Application.DirectExpenses.RegisterDirectExpense;
using Hearth.Application.FixedExpenses.RegisterFixedExpense;
using Hearth.Application.FixedIncomes.ConfirmFixedIncomeReceipt;
using Hearth.Application.FixedIncomes.CreateFixedIncome;
using Hearth.Application.Incomes.RegisterIncome;
using Hearth.Domain;
using Hearth.Web.Components.Pages;

namespace Hearth.Tests.Web.Pages;

public sealed class ActivitiesPageTests : WebTestBase
{
    [Fact]
    public async Task ActivitiesPage_RendersUnifiedMovementsAndFiltersByTypeSearchAndAllMonths()
    {
        Clock.Today = new DateOnly(2026, 11, 25);
        var accountId = await SeedAccountAsync(initialBalance: 1000m, initialBalanceDate: new DateOnly(2026, 10, 1));
        var cardId = await SeedCreditCardAsync(accountId, closingDay: 2, dueDay: 14, name: "Cartão BB", limit: 5000m);

        await Executor.ExecuteAsync<RegisterIncomeHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterIncomeCommand(accountId, new DateOnly(2026, 11, 5), 2500m, "Salário Nov"), ct),
            showErrorSnackbar: false);

        var fixedIncomeResult = await Executor.ExecuteAsync<CreateFixedIncomeHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new CreateFixedIncomeCommand(accountId, 2026, 11, null, null, 10, 1200m, "Aluguel Fixo"), ct),
            showErrorSnackbar: false);
        await Executor.ExecuteAsync<ConfirmFixedIncomeReceiptHandler, Hearth.Application.FixedIncomes.FixedIncomeReceiptDto>(
            (h, ct) => h.ExecuteAsync(new ConfirmFixedIncomeReceiptCommand(fixedIncomeResult.Value, 2026, 11, new DateOnly(2026, 11, 10), 1250m), ct),
            showErrorSnackbar: false);

        await Executor.ExecuteAsync<RegisterDirectExpenseHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterDirectExpenseCommand(accountId, new DateOnly(2026, 11, 12), 180m, "Padaria Central", SystemCategories.FoodId), ct),
            showErrorSnackbar: false);

        await Executor.ExecuteAsync<RegisterFixedExpenseHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterFixedExpenseCommand(accountId, 2026, 11, null, null, 110m, "Fibra Óptica", SystemCategories.ServicesId), ct),
            showErrorSnackbar: false);

        // Card purchase on Oct 10 (3x 100): Nov 14 (1/3), Dec 14 (2/3), Jan 14 (3/3)
        await Executor.ExecuteAsync<RegisterCreditCardPurchaseHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterCreditCardPurchaseCommand(
                accountId, cardId, new DateOnly(2026, 10, 10), 300m, 3, 1,
                SystemPurchaseConditions.OwnId, "Monitor Ultrawide", SystemCategories.ShoppingId), ct),
            showErrorSnackbar: false);

        var cut = Render<ActivitiesPage>();

        Assert.Equal("Atividades", cut.Find("h1").TextContent.Trim());
        var tableText = cut.Find("[data-testid='activities-table']").TextContent;
        Assert.Contains("Salário Nov", tableText);
        Assert.Contains("Aluguel Fixo", tableText);
        Assert.Contains("Padaria Central", tableText);
        Assert.Contains("Fibra Óptica", tableText);
        Assert.Contains("Monitor Ultrawide", tableText);
        Assert.Contains("1/3", cut.Find("[data-testid='activity-installment-badge']").TextContent);

        // Filter by Receitas only (includes both one-off incomes and fixed incomes)
        await cut.Find("[data-testid='activities-type-income']").ClickAsync();
        var incomeOnlyText = cut.Find("[data-testid='activities-table']").TextContent;
        Assert.Contains("Salário Nov", incomeOnlyText);
        Assert.Contains("Aluguel Fixo", incomeOnlyText);
        Assert.DoesNotContain("Padaria Central", incomeOnlyText);

        // Filter by Cartão only
        await cut.Find("[data-testid='activities-type-card']").ClickAsync();
        var cardOnlyText = cut.Find("[data-testid='activities-table']").TextContent;
        Assert.Contains("Monitor Ultrawide", cardOnlyText);
        Assert.DoesNotContain("Salário Nov", cardOnlyText);

        // Reset to Todas and filter by client-side search
        await cut.Find("[data-testid='activities-type-all']").ClickAsync();
        cut.Find("[data-testid='activities-search']").Input("Padaria");
        var searchText = cut.Find("[data-testid='activities-table']").TextContent;
        Assert.Contains("Padaria Central", searchText);
        Assert.DoesNotContain("Fibra Óptica", searchText);
    }
}
