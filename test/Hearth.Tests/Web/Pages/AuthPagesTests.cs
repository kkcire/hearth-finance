using System.Security.Claims;
using Bunit;
using Hearth.Application.Auth;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Web.Auth;
using Hearth.Web.Components.Pages;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hearth.Tests.Web.Pages;

public sealed class AuthPagesTests : WebTestBase
{
    private readonly RecordingAuthenticationService _authService = new();

    private DefaultHttpContext CreateHttpContext(ClaimsPrincipal? user = null)
    {
        var context = new DefaultHttpContext();
        var sp = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(_authService)
            .BuildServiceProvider();
        context.RequestServices = sp;
        if (user is not null)
        {
            context.User = user;
        }

        return context;
    }

    private async Task<User> SeedUserAsync(
        string name = "Erick",
        string email = "erick@hearth.app",
        string password = "password123")
    {
        var user = User.Create(
            Guid.CreateVersion7(),
            name,
            email,
            PasswordHasher.Hash(password),
            Clock.UtcNow);
        await Users.AddAsync(user);
        return user;
    }

    [Fact]
    public void LoginPage_RendersAccessibleFieldsAndHidesRegisterLinkWhenRegistrationDisabled()
    {
        AuthOptions.AllowRegistration = false;
        var httpContext = CreateHttpContext();

        var cut = Render<LoginPage>(p => p.AddCascadingValue(httpContext));

        Assert.Equal("email", cut.Find("[data-testid='login-email-input']").GetAttribute("autocomplete"));
        Assert.Equal("current-password", cut.Find("[data-testid='login-password-input']").GetAttribute("autocomplete"));
        Assert.Empty(cut.FindAll("[data-testid='go-to-register']"));
    }

    [Fact]
    public void LoginPage_ShowsRegisterLinkWhenRegistrationEnabled()
    {
        AuthOptions.AllowRegistration = true;
        var httpContext = CreateHttpContext();

        var cut = Render<LoginPage>(p => p.AddCascadingValue(httpContext));

        Assert.Single(cut.FindAll("[data-testid='go-to-register']"));
    }

    [Fact]
    public void LoginPage_AlreadyAuthenticated_RedirectsToSafeLocalReturnUrl()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/login?returnUrl=%2Fcartao%2Ffaturas");

        var principal = HearthAuth.CreatePrincipal(new UserDto(Guid.CreateVersion7(), "Erick", "erick@hearth.app"));
        var httpContext = CreateHttpContext(principal);

        Render<LoginPage>(p => p.AddCascadingValue(httpContext));

        Assert.EndsWith("/cartao/faturas", nav.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoginPage_WrongPassword_ShowsGenericErrorKeepsEmailAndClearsPassword()
    {
        await SeedUserAsync();
        var httpContext = CreateHttpContext();

        var cut = Render<LoginPage>(p => p.AddCascadingValue(httpContext));
        cut.Instance.Input!.Email = "erick@hearth.app";
        cut.Instance.Input!.Password = "senha-errada";

        await cut.Find("form").SubmitAsync();

        Assert.Contains("E-mail ou senha inválidos.", cut.Find("[data-testid='login-error']").TextContent);
        Assert.Equal("erick@hearth.app", cut.Find("[data-testid='login-email-input']").GetAttribute("value"));
        Assert.Equal(string.Empty, cut.Find("[data-testid='login-password-input']").GetAttribute("value"));
        Assert.Null(_authService.SignedInPrincipal);
    }

    [Fact]
    public async Task LoginPage_ValidCredentialsWithoutAccount_SignsInAndRedirectsToOnboarding()
    {
        var user = await SeedUserAsync();
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/login?returnUrl=%2Fcartao%2Ffaturas");
        var httpContext = CreateHttpContext();

        var cut = Render<LoginPage>(p => p.AddCascadingValue(httpContext));
        cut.Instance.Input!.Email = "ERICK@hearth.app";
        cut.Instance.Input!.Password = "password123";

        await cut.Find("form").SubmitAsync();

        Assert.NotNull(_authService.SignedInPrincipal);
        Assert.True(HearthAuth.TryReadUser(_authService.SignedInPrincipal, out var signedInId, out _, out _));
        Assert.Equal(user.Id, signedInId);
        Assert.EndsWith("/onboarding", nav.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoginPage_ValidCredentialsWithAccount_SignsInAndRedirectsToSafeReturnUrl()
    {
        var user = await SeedUserAsync();
        await Accounts.AddAsync(Account.Create(
            Guid.CreateVersion7(), user.Id, "Principal", Money.Zero, new DateOnly(2026, 1, 1), CardBillingRule.Of(3, 12)));

        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/login?returnUrl=%2Fcartao%2Ffaturas");
        var httpContext = CreateHttpContext();

        var cut = Render<LoginPage>(p => p.AddCascadingValue(httpContext));
        cut.Instance.Input!.Email = "erick@hearth.app";
        cut.Instance.Input!.Password = "password123";

        await cut.Find("form").SubmitAsync();

        Assert.NotNull(_authService.SignedInPrincipal);
        Assert.EndsWith("/cartao/faturas", nav.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoginPage_ValidCredentialsWithExternalReturnUrl_IgnoresOpenRedirectAndGoesRoot()
    {
        var user = await SeedUserAsync();
        await Accounts.AddAsync(Account.Create(
            Guid.CreateVersion7(), user.Id, "Principal", Money.Zero, new DateOnly(2026, 1, 1), CardBillingRule.Of(3, 12)));

        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/login?returnUrl=https%3A%2F%2Fevil.com");
        var httpContext = CreateHttpContext();

        var cut = Render<LoginPage>(p => p.AddCascadingValue(httpContext));
        cut.Instance.Input!.Email = "erick@hearth.app";
        cut.Instance.Input!.Password = "password123";

        await cut.Find("form").SubmitAsync();

        Assert.EndsWith("/", nav.Uri, StringComparison.Ordinal);
        Assert.DoesNotContain("evil.com", nav.Uri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RegisterPage_WhenRegistrationDisabled_ShowsNoticeAndHidesForm()
    {
        AuthOptions.AllowRegistration = false;
        var httpContext = CreateHttpContext();

        var cut = Render<RegisterPage>(p => p.AddCascadingValue(httpContext));

        Assert.NotEmpty(cut.FindAll("[data-testid='registration-disabled']"));
        Assert.Empty(cut.FindAll("[data-testid='register-submit']"));
        Assert.Single(cut.FindAll("[data-testid='go-to-login']"));
    }

    [Fact]
    public void RegisterPage_WhenRegistrationEnabled_RendersSingleScreenWithConfirmPasswordAndHelperText()
    {
        AuthOptions.AllowRegistration = true;
        var httpContext = CreateHttpContext();

        var cut = Render<RegisterPage>(p => p.AddCascadingValue(httpContext));

        Assert.Equal("name", cut.Find("[data-testid='register-name-input']").GetAttribute("autocomplete"));
        Assert.Equal("email", cut.Find("[data-testid='register-email-input']").GetAttribute("autocomplete"));
        Assert.Equal("new-password", cut.Find("[data-testid='register-password-input']").GetAttribute("autocomplete"));
        Assert.Equal("new-password", cut.Find("[data-testid='register-confirm-password-input']").GetAttribute("autocomplete"));
        Assert.Contains("Mínimo de 8 caracteres.", cut.Markup);
    }

    [Fact]
    public async Task RegisterPage_ValidSingleWordName_CreatesUserSignsInAndRedirectsToOnboarding()
    {
        AuthOptions.AllowRegistration = true;
        var nav = Services.GetRequiredService<NavigationManager>();
        var httpContext = CreateHttpContext();

        var cut = Render<RegisterPage>(p => p.AddCascadingValue(httpContext));
        cut.Instance.Input!.Name = "Erick";
        cut.Instance.Input!.Email = "erick@hearth.app";
        cut.Instance.Input!.Password = "segredo123";
        cut.Instance.Input!.ConfirmPassword = "segredo123";

        await cut.Find("form").SubmitAsync();

        var created = Assert.Single(Users.Users);
        Assert.Equal("Erick", created.Name);
        Assert.Equal("erick@hearth.app", created.Email);
        Assert.NotNull(_authService.SignedInPrincipal);
        Assert.EndsWith("/onboarding", nav.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisterPage_MismatchedConfirmPassword_ShowsFieldErrorKeepsNameAndEmailAndClearsPasswords()
    {
        AuthOptions.AllowRegistration = true;
        var httpContext = CreateHttpContext();

        var cut = Render<RegisterPage>(p => p.AddCascadingValue(httpContext));
        cut.Instance.Input!.Name = "Erick";
        cut.Instance.Input!.Email = "erick@hearth.app";
        cut.Instance.Input!.Password = "segredo123";
        cut.Instance.Input!.ConfirmPassword = "diferente123";

        await cut.Find("form").SubmitAsync();

        Assert.Empty(Users.Users);
        Assert.Contains("As senhas não conferem.", cut.Markup);
        Assert.Equal("Erick", cut.Find("[data-testid='register-name-input']").GetAttribute("value"));
        Assert.Equal("erick@hearth.app", cut.Find("[data-testid='register-email-input']").GetAttribute("value"));
        Assert.Equal(string.Empty, cut.Find("[data-testid='register-password-input']").GetAttribute("value"));
        Assert.Equal(string.Empty, cut.Find("[data-testid='register-confirm-password-input']").GetAttribute("value"));
    }

    [Fact]
    public async Task RegisterPage_DuplicateEmail_ShowsEmailErrorKeepsNameAndEmailAndClearsPasswords()
    {
        AuthOptions.AllowRegistration = true;
        await SeedUserAsync(email: "erick@hearth.app");
        var httpContext = CreateHttpContext();

        var cut = Render<RegisterPage>(p => p.AddCascadingValue(httpContext));
        cut.Instance.Input!.Name = "Novo";
        cut.Instance.Input!.Email = "ERICK@hearth.app";
        cut.Instance.Input!.Password = "segredo123";
        cut.Instance.Input!.ConfirmPassword = "segredo123";

        await cut.Find("form").SubmitAsync();

        Assert.Single(Users.Users);
        Assert.Contains("Já existe uma conta com este e-mail.", cut.Markup);
        Assert.Equal("Novo", cut.Find("[data-testid='register-name-input']").GetAttribute("value"));
        Assert.Equal("ERICK@hearth.app", cut.Find("[data-testid='register-email-input']").GetAttribute("value"));
        Assert.Equal(string.Empty, cut.Find("[data-testid='register-password-input']").GetAttribute("value"));
    }

    private sealed class RecordingAuthenticationService : IAuthenticationService
    {
        public ClaimsPrincipal? SignedInPrincipal { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            SignedInPrincipal = principal;
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;
    }
}
