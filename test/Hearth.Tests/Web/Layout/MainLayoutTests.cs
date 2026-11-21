using Bunit;
using Hearth.Web.Components.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Hearth.Tests.Web.Layout;

public sealed class MainLayoutTests : WebTestBase
{
    [Fact]
    public void Render_WhenNoAccountExists_RedirectsToOnboardingWithoutRenderingProtectedBody()
    {
        var navigation = Services.GetRequiredService<NavigationManager>();
        var renderedUris = new List<string>();
        RenderFragment body = builder =>
        {
            renderedUris.Add(navigation.Uri);
            builder.AddMarkupContent(0, "<p data-testid='protected-body'>Protegido</p>");
        };

        var cut = Render<MainLayout>(parameters => parameters.Add(layout => layout.Body, body));

        cut.WaitForAssertion(() => Assert.EndsWith("/onboarding", navigation.Uri, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(renderedUris, uri => !uri.EndsWith("/onboarding", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Render_WhenAccountExists_ShowsGroupedMenuAndBody()
    {
        await SeedAccountAsync("Conta Erick");

        RenderFragment body = builder =>
            builder.AddMarkupContent(0, "<p data-testid='dashboard-body'>Conteudo</p>");

        var cut = Render<MainLayout>(parameters => parameters.Add(layout => layout.Body, body));

        cut.WaitForAssertion(() => Assert.Contains("Conteudo", cut.Markup));
        Assert.Contains("Conta Erick", cut.Markup);
        Assert.Contains("Painel", cut.Markup);
        Assert.Contains("Cartão", cut.Markup);
        Assert.Contains("Faturas", cut.Markup);
        Assert.Contains("Compras", cut.Markup);
        Assert.Contains("Lançamentos", cut.Markup);
        Assert.Contains("Planejamento", cut.Markup);
        Assert.Contains("Conta", cut.Markup);
    }

    [Fact]
    public async Task Render_WhenChildThrows_ErrorBoundaryShowsFallbackAndRecovers()
    {
        await SeedAccountAsync("Conta Erick");

        var shouldThrow = true;
        RenderFragment body = builder =>
        {
            builder.OpenComponent<FaultyChild>(0);
            builder.AddAttribute(1, nameof(FaultyChild.ShouldThrow), shouldThrow);
            builder.CloseComponent();
        };

        var cut = Render<MainLayout>(parameters => parameters.Add(layout => layout.Body, body));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='error-boundary-alert']")));
        Assert.Contains("Algo deu errado ao exibir esta tela.", cut.Markup);

        shouldThrow = false;
        cut.Find("[data-testid='error-boundary-recover']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Recuperado", cut.Markup));
    }

    private sealed class FaultyChild : ComponentBase
    {
        [Parameter]
        public bool ShouldThrow { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("Simulated UI failure.");
            }

            builder.AddContent(0, "Recuperado");
        }
    }
}
