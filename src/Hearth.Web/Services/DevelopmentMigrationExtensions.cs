using Hearth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Hearth.Web.Services;

public static class DevelopmentMigrationExtensions
{
    private static readonly HashSet<string> LocalHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhost",
        "127.0.0.1",
        "::1",
    };

    public static bool IsLocalHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        return LocalHosts.Contains(host.Trim());
    }

    public static async Task ApplyDevelopmentMigrationsAsync(
        this WebApplication app,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var host = builder.Host;
        var database = builder.Database;

        if (!IsLocalHost(host))
        {
            app.Logger.LogWarning(
                "Skipping automatic EF Core migration because host '{Host}' is not local.",
                host);
            return;
        }

        app.Logger.LogInformation(
            "Applying EF Core migrations to local PostgreSQL host '{Host}', database '{Database}'.",
            host,
            database);

        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HearthDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
