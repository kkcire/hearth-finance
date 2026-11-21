using Bunit;
using Hearth.Application.DirectExpenses.RegisterDirectExpense;
using Hearth.Web.Components.Pages;
using MudBlazor;

namespace Hearth.Tests.Web.Pages;

public sealed class CategoriesPageTests : WebTestBase
{
    [Fact]
    public async Task Lifecycle_CreateRenameArchiveRestoreAndDeleteCategory()
    {
        var accountId = await SeedAccountAsync();
        var cut = Render<CategoriesPage>();

        cut.WaitForAssertion(() => Assert.Contains("Alimentação", cut.Markup));

        // Create
        cut.Find("input[data-testid='new-category-name']").Input("Pets");
        await cut.Find("[data-testid='create-category-submit']").ClickAsync();

        var pets = (await Categories.GetByAccountAsync(accountId)).Single(c => c.Name == "Pets");
        Assert.Contains("Pets", cut.Markup);

        // Rename
        await cut.Find($"[data-testid='rename-{pets.Id}']").ClickAsync();
        cut.Find("input[data-testid='rename-category-input']").Input("Animais");
        await cut.Find("[data-testid='rename-category-save']").ClickAsync();

        var tableBody = cut.Find("tbody").TextContent;
        Assert.Contains("Animais", tableBody);
        Assert.DoesNotContain("Pets", tableBody);

        // Archive (hidden by default)
        await cut.Find($"[data-testid='archive-{pets.Id}']").ClickAsync();
        Assert.DoesNotContain("Animais", cut.Find("tbody").TextContent);

        // Show archived -> visible with badge -> restore
        cut.Find("input[type='checkbox']").Change(true);
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Animais", cut.Markup);
            Assert.Contains("Arquivada", cut.Markup);
        });

        await cut.Find($"[data-testid='restore-{pets.Id}']").ClickAsync();
        var restored = await Categories.GetByIdAsync(pets.Id);
        Assert.False(restored!.IsArchived);

        // Delete with inline confirmation
        await cut.Find($"[data-testid='delete-{pets.Id}']").ClickAsync();
        await cut.Find($"[data-testid='confirm-delete-{pets.Id}']").ClickAsync();

        Assert.Null(await Categories.GetByIdAsync(pets.Id));
    }

    [Fact]
    public async Task Delete_WhenCategoryIsInUse_ShowsWarningAndKeepsCategory()
    {
        var accountId = await SeedAccountAsync();
        var category = (await Categories.GetByAccountAsync(accountId)).First();

        var registerResult = await Executor.ExecuteAsync<RegisterDirectExpenseHandler, Guid>(
            (handler, ct) => handler.ExecuteAsync(
                new RegisterDirectExpenseCommand(accountId, Clock.Today, 50m, "Mercado", category.Id), ct),
            showErrorSnackbar: false);
        Assert.True(registerResult.IsSuccess);

        var cut = Render<CategoriesPage>();
        cut.WaitForAssertion(() => Assert.Contains(category.Name, cut.Markup));

        await cut.Find($"[data-testid='delete-{category.Id}']").ClickAsync();
        await cut.Find($"[data-testid='confirm-delete-{category.Id}']").ClickAsync();

        Assert.NotNull(await Categories.GetByIdAsync(category.Id));
        Assert.Contains(Snackbar.ShownSnackbars, s => s.Severity == Severity.Warning);
    }
}
