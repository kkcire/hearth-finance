using Bunit;
using Hearth.Application.Categories.ArchiveCategory;
using Hearth.Application.Categories.CreateCategory;
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
    public async Task IncomesPage_CreateReceiveRevertAndDelete()
    {
        var accountId = await SeedAccountAsync();
        var cut = Render<IncomesPage>();

        await cut.Find("[data-testid='new-income-button']").ClickAsync();
        cut.Find("input[data-testid='income-description-input']").Input("Salário");
        cut.Find("input[data-testid='income-amount-input']").Change("5.500,00");
        await cut.Find("[data-testid='save-income']").ClickAsync();

        var income = Assert.Single(await Incomes.GetByAccountAsync(accountId));
        Assert.False(income.IsReceived);
        Assert.Equal(Money.FromDecimal(5500m), income.Amount);

        await cut.Find($"[data-testid='receive-income-{income.Id}']").ClickAsync();
        await cut.Find("[data-testid='confirm-receive-submit']").ClickAsync();

        var received = await Incomes.GetByIdAsync(income.Id);
        Assert.True(received!.IsReceived);

        await cut.Find($"[data-testid='revert-income-{income.Id}']").ClickAsync();
        var reverted = await Incomes.GetByIdAsync(income.Id);
        Assert.False(reverted!.IsReceived);

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
}
