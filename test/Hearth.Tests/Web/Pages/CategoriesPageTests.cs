using Bunit;
using Hearth.Application.Categories.CreateCategory;
using Hearth.Application.DirectExpenses.RegisterDirectExpense;
using Hearth.Web.Components.Pages;
using MudBlazor;

namespace Hearth.Tests.Web.Pages;

public sealed class CategoriesPageTests : WebTestBase
{
    [Fact]
    public async Task Lifecycle_CreateRenameArchiveRestoreAndDeleteCategory()
    {
        await SeedAccountAsync();
        var cut = Render<CategoriesPage>();

        cut.WaitForAssertion(() => Assert.Contains("Alimentação", cut.Markup));

        // Create
        cut.Find("input[data-testid='new-category-name']").Input("Pets");
        await cut.Find("[data-testid='create-category-submit']").ClickAsync();

        var pets = (await Categories.GetVisibleForUserAsync(CurrentUser.UserId!.Value)).Single(c => c.Name == "Pets");
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
        var customResult = await Executor.ExecuteAsync<CreateCategoryHandler, Guid>(
            (handler, ct) => handler.ExecuteAsync(new CreateCategoryCommand("Mercado Extra"), ct),
            showErrorSnackbar: false);
        var categoryId = customResult.Value;

        var registerResult = await Executor.ExecuteAsync<RegisterDirectExpenseHandler, Guid>(
            (handler, ct) => handler.ExecuteAsync(
                new RegisterDirectExpenseCommand(accountId, Clock.Today, 50m, "Mercado", categoryId), ct),
            showErrorSnackbar: false);
        Assert.True(registerResult.IsSuccess);

        var cut = Render<CategoriesPage>();
        cut.WaitForAssertion(() => Assert.Contains("Mercado Extra", cut.Markup));

        await cut.Find($"[data-testid='delete-{categoryId}']").ClickAsync();
        await cut.Find($"[data-testid='confirm-delete-{categoryId}']").ClickAsync();

        Assert.NotNull(await Categories.GetByIdAsync(categoryId));
        Assert.Contains(Snackbar.ShownSnackbars, s => s.Severity == Severity.Warning);
    }
}
