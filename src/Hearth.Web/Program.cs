using System.Globalization;
using Hearth.Application;
using Hearth.Application.Abstractions;
using Hearth.Infrastructure;
using Hearth.Infrastructure.Time;
using Hearth.Web.Components;
using Hearth.Web.Services;
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
    timeZoneId = SystemClock.DefaultTimeZoneId;
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString, timeZoneId);
builder.Services.AddMudServices();
builder.Services.AddLocalization();

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
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
