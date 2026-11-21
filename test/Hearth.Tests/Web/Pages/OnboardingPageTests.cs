using Bunit;
using Hearth.Domain.ValueObjects;
using Hearth.Web.Components.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Hearth.Tests.Web.Pages;

public sealed class OnboardingPageTests : WebTestBase
{
    [Fact]
    public async Task Submit_WithPortugueseDecimalInitialBalance_CreatesAccountWithDefaultCategoriesAndNavigatesHome()
    {
        var cut = Render<OnboardingPage>();

        cut.Find("input[data-testid='account-name-input']").Input("Conta Erick");
        cut.Find("input[data-testid='initial-balance-input']").Change("1.234,56");
        cut.Find("input[data-testid='closing-day-input']").Change("5");
        cut.Find("input[data-testid='due-day-input']").Change("15");

        await cut.Find("[data-testid='create-account-submit']").ClickAsync();

        var navigation = Services.GetRequiredService<NavigationManager>();
        var savedAccounts = await Accounts.GetAllAsync();

        var created = Assert.Single(savedAccounts);
        Assert.Equal("Conta Erick", created.Name);
        Assert.Equal(Money.FromDecimal(1234.56m), created.InitialBalance);
        Assert.Equal(Clock.Today, created.InitialBalanceDate);
        Assert.Equal(5, created.BillingRule.ClosingDay);
        Assert.Equal(15, created.BillingRule.DueDay);

        var categories = await Categories.GetByAccountAsync(created.Id);
        Assert.Equal(8, categories.Count);

        Assert.Equal(created.Id, AccountContext.CurrentAccountId);
        Assert.EndsWith("/", navigation.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Submit_WithEmptyName_ShowsValidationErrorAndDoesNotCreateAccount()
    {
        var cut = Render<OnboardingPage>();

        await cut.Find("[data-testid='create-account-submit']").ClickAsync();

        Assert.Empty(await Accounts.GetAllAsync());
        Assert.Contains("Informe o nome da conta.", cut.Markup);
    }
}
