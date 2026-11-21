using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hearth.Infrastructure.Persistence;

/// <summary>
/// Allows <c>dotnet ef</c> to create <see cref="HearthDbContext"/> at design time without
/// requiring the web host to be configured.
/// </summary>
public sealed class HearthDbContextFactory : IDesignTimeDbContextFactory<HearthDbContext>
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5433;Database=hearth;Username=hearth;Password=hearth";

    public HearthDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("HEARTH_CONNECTION_STRING")
                               ?? DefaultConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<HearthDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new HearthDbContext(optionsBuilder.Options);
    }
}
