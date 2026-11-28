using Bunit;
using Hearth.Application.Categories.ArchiveCategory;
using Hearth.Application.Categories.CreateCategory;
using Hearth.Application.DirectExpenses.RegisterDirectExpense;
using Hearth.Domain;
using Hearth.Domain.ValueObjects;
using Hearth.Web.Components.Pages;
using MudBlazor;

namespace Hearth.Tests.Web.Pages;

public sealed class EntriesPagesTests : WebTestBase
{
    [Fact]
    public async Task DirectExpensesPage_CreateEditDeleteAndHideArchivedCategory()
    {
        var accountId = await SeedAccountAsync();
        var customResult = await Executor.ExecuteAsync<CreateCategoryHandler, Guid>(
            (handler, ct) => handler.ExecuteAsync(new CreateCategoryCommand("Temporária"), ct),
            showErrorSnackbar: false);
        var archivedId = customResult.Value;

        await Executor.ExecuteAsync<ArchiveCategoryHandler, bool>(
            (handler, ct) => handler.ExecuteAsync(new ArchiveCategoryCommand(archivedId), ct),
            showErrorSnackbar: false);

        Render<MudPopoverProvider>();
        var cut = Render<DirectExpensesPage>();

        await cut.Find("[data-testid='new-direct-expense-button']").ClickAsync();

        var offeredCategoryIds = cut.FindComponent<MudSelect<Guid?>>()
            .FindComponents<MudSelectItem<Guid?>>()
            .Select(item => item.Instance.Value)
            .ToList();
        Assert.DoesNotContain(archivedId, offeredCategoryIds);

        cut.Find("input[data-testid='direct-description-input']").Input("Padaria");
        cut.Find("input[data-testid='direct-amount-input']").Change("45,90");
        await cut.Find("[data-testid='save-direct-expense']").ClickAsync();

        var created = Assert.Single(await DirectExpenses.GetByAccountAsync(accountId));
        Assert.Equal("Padaria", created.Description);
        Assert.Equal(Money.FromDecimal(45.90m), created.Amount);

        await cut.Find($"[data-testid='edit-direct-{created.Id}']").ClickAsync();
        cut.Find("input[data-testid='direct-description-input']").Input("Padaria Centro");
        await cut.Find("[data-testid='save-direct-expense']").ClickAsync();

        var updated = await DirectExpenses.GetByIdAsync(created.Id);
        Assert.Equal("Padaria Centro", updated!.Description);

        await cut.Find($"[data-testid='delete-direct-{created.Id}']").ClickAsync();
        await cut.Find($"[data-testid='confirm-delete-direct-{created.Id}']").ClickAsync();

        Assert.Empty(await DirectExpenses.GetByAccountAsync(accountId));
    }

    [Fact]
    public async Task IncomesPage_CreateEditDateDerivesStatusAndDelete()
    {
        Clock.Today = new DateOnly(2026, 11, 20);
        var accountId = await SeedAccountAsync();
        Render<MudPopoverProvider>();
        var cut = Render<IncomesPage>();

        await cut.Find("[data-testid='new-income-button']").ClickAsync();
        // Single date picker only — no secondary received date or manual receive/revert controls
        Assert.NotEmpty(cut.FindAll("input[data-testid='income-date-input']"));
        Assert.Empty(cut.FindAll("input[data-testid='income-received-date-input']"));
        Assert.Empty(cut.FindAll("[data-testid='receive-income-panel']"));

        cut.Find("input[data-testid='income-description-input']").Input("Salário");
        cut.Find("input[data-testid='income-amount-input']").Change("5.500,00");
        await cut.Find("[data-testid='save-income']").ClickAsync();

        var income = Assert.Single(await Incomes.GetByAccountAsync(accountId));
        Assert.Equal(Clock.Today, income.Date);
        Assert.True(income.IsReceivedOn(Clock.Today));
        Assert.Equal(Money.FromDecimal(5500m), income.Amount);
        Assert.Equal("Recebida", cut.Find($"[data-testid='income-status-{income.Id}']").TextContent.Trim());
        Assert.Empty(cut.FindAll($"[data-testid='receive-income-{income.Id}']"));
        Assert.Empty(cut.FindAll($"[data-testid='revert-income-{income.Id}']"));

        // Edit date to future (25/11/2026) and change amount freely -> status flips to "Prevista"
        await cut.Find($"[data-testid='edit-income-{income.Id}']").ClickAsync();
        cut.Find("input[data-testid='income-amount-input']").Change("5.800,00");
        cut.Find("input[data-testid='income-date-input']").Change("25/11/2026");
        await cut.Find("[data-testid='save-income']").ClickAsync();

        var updated = await Incomes.GetByIdAsync(income.Id);
        Assert.Equal(new DateOnly(2026, 11, 25), updated!.Date);
        Assert.Equal(Money.FromDecimal(5800m), updated.Amount);
        Assert.False(updated.IsReceivedOn(Clock.Today));
        Assert.Equal("Prevista", cut.Find($"[data-testid='income-status-{income.Id}']").TextContent.Trim());

        await cut.Find($"[data-testid='delete-income-{income.Id}']").ClickAsync();
        await cut.Find($"[data-testid='confirm-delete-income-{income.Id}']").ClickAsync();
        Assert.Empty(await Incomes.GetByAccountAsync(accountId));
    }

    [Fact]
    public async Task FixedExpensesPage_OnlyOffersPaymentInActiveMonthsAndSupportsPayAndRevert()
    {
        var accountId = await SeedAccountAsync();
        Render<MudPopoverProvider>();
        var cut = Render<FixedExpensesPage>();

        await cut.Find("[data-testid='new-fixed-expense-button']").ClickAsync();
        cut.Find("input[data-testid='fixed-description-input']").Input("Aluguel");
        cut.Find("input[data-testid='fixed-amount-input']").Change("1.800,00");
        await cut.Find("[data-testid='create-fixed-submit']").ClickAsync();

        var expense = Assert.Single(await FixedExpenses.GetByAccountAsync(accountId));
        Assert.NotEmpty(cut.FindAll($"[data-testid='pay-fixed-{expense.Id}']"));
        // Redundant per-field quick-edit icons are removed; single Edit (pencil) button centralizes details/amount/end
        Assert.NotEmpty(cut.FindAll($"[data-testid='edit-fixed-{expense.Id}']"));
        Assert.Empty(cut.FindAll($"[data-testid='change-amount-fixed-{expense.Id}']"));
        Assert.Empty(cut.FindAll($"[data-testid='end-fixed-{expense.Id}']"));

        // Pay active month (Nov/2026)
        await cut.Find($"[data-testid='pay-fixed-{expense.Id}']").ClickAsync();
        await cut.Find("[data-testid='confirm-fixed-payment']").ClickAsync();

        var paid = await FixedExpenses.GetByIdAsync(expense.Id);
        Assert.True(paid!.Payments.ContainsKey(YearMonth.Of(2026, 11)));

        // Navigate to previous month (Oct/2026) where expense is inactive: payment button must not appear
        await cut.Find("[data-testid='month-prev']").ClickAsync();
        Assert.Empty(cut.FindAll($"[data-testid='pay-fixed-{expense.Id}']"));
        Assert.Empty(cut.FindAll($"[data-testid='revert-fixed-{expense.Id}']"));

        // Back to Nov/2026 and revert payment
        await cut.Find("[data-testid='month-next']").ClickAsync();
        await cut.Find($"[data-testid='revert-fixed-{expense.Id}']").ClickAsync();

        var reverted = await FixedExpenses.GetByIdAsync(expense.Id);
        Assert.Empty(reverted!.Payments);
    }

    [Fact]
    public async Task DirectExpensesPage_SearchInputFiltersByDescriptionAndCategory()
    {
        var accountId = await SeedAccountAsync();
        await Executor.ExecuteAsync<RegisterDirectExpenseHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterDirectExpenseCommand(accountId, new DateOnly(2026, 11, 5), 65m, "Padaria Central", SystemCategories.FoodId), ct),
            showErrorSnackbar: false);
        await Executor.ExecuteAsync<RegisterDirectExpenseHandler, Guid>(
            (h, ct) => h.ExecuteAsync(new RegisterDirectExpenseCommand(accountId, new DateOnly(2026, 11, 8), 120m, "Uber Aeroporto", SystemCategories.TransportId), ct),
            showErrorSnackbar: false);

        var cut = Render<DirectExpensesPage>();

        Assert.Contains("Padaria Central", cut.Markup);
        Assert.Contains("Uber Aeroporto", cut.Markup);

        cut.Find("input[data-testid='direct-expense-search']").Input("Padaria");

        Assert.Contains("Padaria Central", cut.Markup);
        Assert.DoesNotContain("Uber Aeroporto", cut.Markup);
        Assert.Contains("65,00", cut.Find("[data-testid='direct-expenses-total']").TextContent);
    }

    [Fact]
    public async Task FixedIncomesPage_CreateConfirmWithBonusRevertAdjustAmountAndDelete()
    {
        Clock.Today = new DateOnly(2026, 11, 20);
        var accountId = await SeedAccountAsync(initialBalanceDate: new DateOnly(2026, 10, 1));
        Render<MudPopoverProvider>();
        var cut = Render<FixedIncomesPage>();

        // Create recurring salary R$ 4.000,00
        await cut.Find("[data-testid='new-fixed-income-button']").ClickAsync();
        cut.Find("input[data-testid='fixed-income-description-input']").Input("Salário CLT");
        cut.Find("input[data-testid='fixed-income-amount-input']").Change("4.000,00");
        await cut.Find("[data-testid='create-fixed-income-submit']").ClickAsync();

        var created = Assert.Single(await FixedIncomes.GetByAccountAsync(accountId));
        Assert.Equal("Salário CLT", created.Description);
        Assert.Equal("Prevista", cut.Find($"[data-testid='fixed-income-status-{created.Id}']").TextContent.Trim());

        // Confirm receipt with R$ 400 bonus (4.400,00)
        await cut.Find($"[data-testid='confirm-fixed-income-{created.Id}']").ClickAsync();
        cut.Find("input[data-testid='confirm-fixed-income-amount-input']").Change("4.400,00");
        await cut.Find("[data-testid='confirm-fixed-income-submit']").ClickAsync();

        var confirmed = await FixedIncomes.GetByIdAsync(created.Id);
        var receipt = confirmed!.GetReceiptFor(YearMonth.Of(2026, 11));
        Assert.NotNull(receipt);
        Assert.Equal(Money.FromDecimal(4400m), receipt!.ActualAmount);
        Assert.Equal("Recebida", cut.Find($"[data-testid='fixed-income-status-{created.Id}']").TextContent.Trim());
        Assert.Contains("4.400,00", cut.Find($"[data-testid='fixed-income-actual-{created.Id}']").TextContent);

        // Revert receipt -> returns to "Prevista"
        await cut.Find($"[data-testid='revert-fixed-income-{created.Id}']").ClickAsync();
        Assert.Equal("Prevista", cut.Find($"[data-testid='fixed-income-status-{created.Id}']").TextContent.Trim());

        // Edit details + apply salary adjustment to R$ 4.500,00
        await cut.Find($"[data-testid='edit-fixed-income-{created.Id}']").ClickAsync();
        cut.Find("input[data-testid='edit-fixed-income-description-input']").Input("Salário Atualizado");
        cut.Find("input[data-testid='edit-fixed-income-amount-input']").Change("4.500,00");
        await cut.Find("[data-testid='save-fixed-income-update']").ClickAsync();

        var updated = await FixedIncomes.GetByIdAsync(created.Id);
        Assert.Equal("Salário Atualizado", updated!.Description);
        Assert.Equal(Money.FromDecimal(4500m), updated.GetExpectedAmountFor(YearMonth.Of(2026, 11)));

        // Delete
        await cut.Find($"[data-testid='delete-fixed-income-{created.Id}']").ClickAsync();
        await cut.Find($"[data-testid='confirm-delete-fixed-income-{created.Id}']").ClickAsync();
        Assert.Empty(await FixedIncomes.GetByAccountAsync(accountId));
    }
}
