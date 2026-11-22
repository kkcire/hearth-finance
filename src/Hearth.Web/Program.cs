using System.Globalization;
using System.Threading.RateLimiting;
using Hearth.Application;
using Hearth.Application.Abstractions;
using Hearth.Infrastructure;
using Hearth.Infrastructure.Time;
using Hearth.Web.Auth;
using Hearth.Web.Components;
using Hearth.Web.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Hearth");
if (string.IsNullOrWhiteSpace(connectionString))
{
    connectionString = Environment.GetEnvironmentVariable("HEARTH_CONNECTION_STRING");
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database connection string 'ConnectionStrings:Hearth' (or environment variable 'HEARTH_CONNECTION_STRING') is not configured.");
}

var timeZoneId = builder.Configuration["Hearth:TimeZoneId"];
if (string.IsNullOrWhiteSpace(timeZoneId))
{
    timeZoneId = Hearth.Infrastructure.Time.SystemClock.DefaultTimeZoneId;
}

builder.Services.Configure<HearthAuthOptions>(
    builder.Configuration.GetSection(HearthAuthOptions.SectionName));

var authOptions = builder.Configuration
    .GetSection(HearthAuthOptions.SectionName)
    .Get<HearthAuthOptions>() ?? new HearthAuthOptions();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString, timeZoneId);
builder.Services.AddMudServices();
builder.Services.AddLocalization();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = HearthAuth.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.IsEssential = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.ReturnUrlParameter = "returnUrl";
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "text/html; charset=utf-8";

        var path = context.HttpContext.Request.Path.Value ?? "/login";
        var backPath = string.Equals(path, "/cadastro", StringComparison.OrdinalIgnoreCase)
            ? "/cadastro"
            : "/login";

        var html = $$"""
            <!DOCTYPE html>
            <html lang="pt-BR">
            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                <title>Muitas tentativas — Hearth</title>
                <style>
                    body { margin: 0; min-height: 100vh; display: flex; align-items: center; justify-content: center; background: #121212; color: rgba(255,255,255,0.92); font-family: Roboto, system-ui, sans-serif; padding: 16px; }
                    .card { max-width: 420px; width: 100%; background: #1E1E1E; border: 1px solid rgba(255,255,255,0.08); border-radius: 12px; padding: 28px; }
                    h1 { margin: 0 0 12px; font-size: 1.25rem; }
                    p { margin: 0 0 20px; color: rgba(255,255,255,0.72); line-height: 1.5; }
                    a { display: inline-block; background: #D32F2F; color: #fff; text-decoration: none; padding: 10px 18px; border-radius: 8px; font-weight: 500; }
                </style>
            </head>
            <body>
                <main class="card">
                    <h1>Muitas tentativas</h1>
                    <p>{{HearthAuth.RateLimitExceededMessage}}</p>
                    <a href="{{backPath}}">Voltar</a>
                </main>
            </body>
            </html>
            """;

        await context.HttpContext.Response.WriteAsync(html, cancellationToken);
    };

    options.AddPolicy(HearthAuth.RateLimitPolicyName, httpContext =>
    {
        var rateLimit = authOptions.AuthRateLimit;
        if (!rateLimit.Enabled || !HttpMethods.IsPost(httpContext.Request.Method))
        {
            return RateLimitPartition.GetNoLimiter("bypass");
        }

        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var permitLimit = Math.Max(1, rateLimit.PermitLimit);
        var windowSeconds = Math.Max(1, rateLimit.WindowSeconds);

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    });
});

builder.Services.AddScoped<ScopedCurrentUser>();
builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<ScopedCurrentUser>());
builder.Services.AddScoped<UseCaseExecutor>();
builder.Services.AddScoped<AccountContext>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

await app.ApplyDevelopmentMigrationsAsync(connectionString);

var ptBr = HearthFormat.PtBr;
CultureInfo.DefaultThreadCurrentCulture = ptBr;
CultureInfo.DefaultThreadCurrentUICulture = ptBr;

var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(ptBr),
    SupportedCultures = [ptBr],
    SupportedUICultures = [ptBr],
};
localizationOptions.RequestCultureProviders.Clear();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseRequestLocalization(localizationOptions);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapPost("/auth/logout", async (HttpContext httpContext, IAntiforgery antiforgery) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(httpContext);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest();
    }

    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/login");
}).RequireAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
