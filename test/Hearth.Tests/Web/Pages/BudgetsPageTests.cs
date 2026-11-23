using Bunit;
using Hearth.Application.Categories.ArchiveCategory;
using Hearth.Application.Categories.CreateCategory;
using Hearth.Domain.ValueObjects;
using Hearth.Web.Components.Pages;
using MudBlazor;

namespace Hearth.Tests.Web.Pages;

public sealed class BudgetsPageTests : WebTestBase
{
    [Fact]
    public async Task CreateChangeAmountEndAndDeleteBudget_WorksEndToEnd()
    {
        var accountId = await SeedAccountAsync();
        var customResult = await Executor.ExecuteAsync<CreateCategoryHandler, Guid>(
            (handler, ct) => handler.ExecuteAsync(new CreateCategoryCommand("Temporária"), ct),
            showErrorSnackbar: false);
        var archivedCategoryId = customResult.Value;

        await Executor.ExecuteAsync<ArchiveCategoryHandler, bool>(
            (handler, ct) => handler.ExecuteAsync(new ArchiveCategoryCommand(archivedCategoryId), ct),
            showErrorSnackbar: false);

        Render<MudPopoverProvider>();
        var cut = Render<BudgetsPage>();

        await cut.Find("[data-testid='toggle-create-budget']").ClickAsync();

        // Archived category must not be offered when creating a budget
        var select = cut.FindComponent<MudSelect<Guid?>>();
        var offeredCategoryIds = select.FindComponents<MudSelectItem<Guid?>>()
            .Select(item => item.Instance.Value)
            .ToList();
        Assert.DoesNotContain(archivedCategoryId, offeredCategoryIds);

        // Create budget with pt-BR decimal
        cut.Find("input[data-testid='budget-amount-input']").Change("1.250,50");
        await cut.Find("[data-testid='create-budget-submit']").ClickAsync();

        var savedBudgets = await CategoryBudgets.GetByAccountAsync(accountId);
        var budget = Assert.Single(savedBudgets);
        Assert.Equal(Money.FromDecimal(1250.50m), budget.AmountIn(YearMonth.From(Clock.Today)));

        // Change amount (takes effect from current calendar month)
        await cut.Find($"[data-testid='change-amount-{budget.Id}']").ClickAsync();
        cut.Find("input[data-testid='edit-budget-amount-input']").Change("1.500,00");
        await cut.Find("[data-testid='save-budget-amount']").ClickAsync();

        var updated = await CategoryBudgets.GetByIdAsync(budget.Id);
        Assert.Equal(Money.FromDecimal(1500.00m), updated!.AmountIn(YearMonth.From(Clock.Today)));

        // End budget
        await cut.Find($"[data-testid='end-budget-{budget.Id}']").ClickAsync();
        await cut.Find("[data-testid='save-budget-end']").ClickAsync();

        var ended = await CategoryBudgets.GetByIdAsync(budget.Id);
        Assert.Equal(YearMonth.From(Clock.Today), ended!.EndMonth);

        // Delete budget
        await cut.Find($"[data-testid='delete-budget-{budget.Id}']").ClickAsync();
        await cut.Find($"[data-testid='confirm-delete-budget-{budget.Id}']").ClickAsync();

        Assert.Empty(await CategoryBudgets.GetByAccountAsync(accountId));
    }
}
