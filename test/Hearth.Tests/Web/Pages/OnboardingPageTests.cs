using Bunit;
using Hearth.Domain.ValueObjects;
using Hearth.Web.Components.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Hearth.Tests.Web.Pages;

public sealed class OnboardingPageTests : WebTestBase
{
    [Fact]
    public void Render_GreetsAuthenticatedUserByName()
    {
        CurrentUser.Name = "Erick";

        var cut = Render<OnboardingPage>();

        Assert.Contains("Olá, Erick!", cut.Find("[data-testid='onboarding-greeting']").TextContent);
    }

    [Fact]
    public async Task Submit_WithPortugueseDecimalInitialBalance_CreatesAccountAndNavigatesHome()
    {
        var cut = Render<OnboardingPage>();

        cut.Find("input[data-testid='account-name-input']").Input("Conta Erick");
        cut.Find("input[data-testid='initial-balance-input']").Change("1.234,56");

        await cut.Find("[data-testid='create-account-submit']").ClickAsync();

        var navigation = Services.GetRequiredService<NavigationManager>();
        var savedAccounts = await Accounts.GetByOwnerAsync(CurrentUser.UserId!.Value);

        var created = Assert.Single(savedAccounts);
        Assert.Equal("Conta Erick", created.Name);
        Assert.Equal(Money.FromDecimal(1234.56m), created.InitialBalance);
        Assert.Equal(Clock.Today, created.InitialBalanceDate);

        var categories = await Categories.GetVisibleForUserAsync(CurrentUser.UserId!.Value);
        Assert.Equal(8, categories.Count);

        Assert.Equal(created.Id, AccountContext.CurrentAccountId);
        Assert.EndsWith("/", navigation.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Submit_WithEmptyName_ShowsValidationErrorAndDoesNotCreateAccount()
    {
        var cut = Render<OnboardingPage>();

        await cut.Find("[data-testid='create-account-submit']").ClickAsync();

        Assert.Empty(await Accounts.GetByOwnerAsync(CurrentUser.UserId!.Value));
        Assert.Contains("Informe o nome da conta.", cut.Markup);
    }
}
