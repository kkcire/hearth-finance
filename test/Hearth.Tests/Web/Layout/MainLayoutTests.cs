using System.Reflection;
using Bunit;
using Hearth.Web.Components;
using Hearth.Web.Components.Layout;
using Hearth.Web.Components.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
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
        Assert.Contains("Lançamentos", cut.Markup);
        Assert.Contains("Atividades", cut.Markup);
        Assert.Contains("Receitas Fixas", cut.Markup);
        Assert.Contains("Planejamento", cut.Markup);
        Assert.Contains("Conta", cut.Markup);
        Assert.Contains("Erick", cut.Find("[data-testid='user-name']").TextContent);

        var logoutForm = cut.Find("form[data-testid='logout-form']");
        Assert.Equal("post", logoutForm.GetAttribute("method"), ignoreCase: true);
        Assert.Equal("/auth/logout", logoutForm.GetAttribute("action"));

        // Navbar bell button present without rendering any notifications drawer/sidebar
        Assert.NotEmpty(cut.FindAll("[data-testid='navbar-notifications-button']"));
        Assert.Empty(cut.FindAll("[data-testid='notifications-drawer']"));
    }

    [Fact]
    public void RedirectToLogin_OnProtectedDeepLink_NavigatesToLoginWithReturnUrl()
    {
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/cartao/faturas");

        Render<RedirectToLogin>();

        Assert.EndsWith("/login?returnUrl=%2Fcartao%2Ffaturas", navigation.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void AllRoutablePages_HaveExplicitAuthorizeOrAllowAnonymousMetadata()
    {
        var allowedAnonymousRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/login",
            "/cadastro",
            "/Error",
            "/not-found",
        };

        var pageTypes = typeof(MainLayout).Assembly
            .GetTypes()
            .Where(type => typeof(IComponent).IsAssignableFrom(type)
                           && type.GetCustomAttributes<RouteAttribute>().Any())
            .ToList();

        Assert.NotEmpty(pageTypes);

        foreach (var pageType in pageTypes)
        {
            var routes = pageType.GetCustomAttributes<RouteAttribute>().Select(r => r.Template).ToList();
            var hasAuthorize = pageType.GetCustomAttributes<AuthorizeAttribute>().Any();
            var hasAllowAnonymous = pageType.GetCustomAttributes<AllowAnonymousAttribute>().Any();

            if (routes.All(route => allowedAnonymousRoutes.Contains(route)))
            {
                Assert.True(hasAllowAnonymous, $"Public page {pageType.Name} must declare [AllowAnonymous].");
            }
            else
            {
                Assert.True(hasAuthorize, $"Protected page {pageType.Name} must declare [Authorize].");
                Assert.False(hasAllowAnonymous, $"Protected page {pageType.Name} must not declare [AllowAnonymous].");
            }
        }
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

    [Fact]
    public async Task NavigateBetweenPages_AlwaysRendersStaticHearthPageTitle()
    {
        var auth = this.AddAuthorization();
        auth.SetAuthorized("Erick");
        Services.AddSingleton<AuthenticationStateProvider>(AuthStateProvider);

        await SeedAccountAsync("Conta Erick");

        var navigation = Services.GetRequiredService<NavigationManager>();
        var cut = Render<StaticTitleTestHost>();

        var routesAndMarkers = new (string Path, string ExpectedHeading)[]
        {
            ("/", "Painel do Mês"),
            ("/cartao", "Cartão de crédito"),
            ("/receitas", "Receitas"),
            ("/conta", "Conta"),
            ("/onboarding", "Olá, Erick!"),
            ("/login", "Entrar"),
            ("/cadastro", "Criar conta"),
        };

        foreach (var (path, expectedHeading) in routesAndMarkers)
        {
            navigation.NavigateTo(path);

            cut.WaitForAssertion(() =>
            {
                Assert.Contains(expectedHeading, cut.Markup);
                var title = cut.Find("title");
                Assert.Equal("Hearth Finance", title.TextContent);
            });
        }
    }

    private sealed class StaticTitleTestHost : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<HeadOutlet>(0);
            builder.CloseComponent();

            builder.OpenComponent<CascadingAuthenticationState>(1);
            builder.AddAttribute(2, nameof(CascadingAuthenticationState.ChildContent), (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<Routes>(0);
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        }
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
