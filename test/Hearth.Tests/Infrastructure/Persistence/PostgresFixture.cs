using Hearth.Infrastructure;
using Hearth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Hearth.Tests.Infrastructure.Persistence;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("hearth_test")
        .WithUsername("hearth")
        .WithPassword("hearth")
        .Build();

    private ServiceProvider? _serviceProvider;

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var services = new ServiceCollection();
        services.AddInfrastructure(ConnectionString);
        _serviceProvider = services.BuildServiceProvider(validateScopes: true);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HearthDbContext>();
        await context.Database.MigrateAsync();
    }

    public AsyncServiceScope CreateScope()
    {
        if (_serviceProvider is null)
        {
            throw new InvalidOperationException("The Postgres fixture has not been initialized.");
        }

        return _serviceProvider.CreateAsyncScope();
    }

    public async Task ResetDatabaseAsync()
    {
        await using var scope = CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HearthDbContext>();

        await context.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE
                fixed_expense_payments,
                fixed_expense_amount_changes,
                fixed_expenses,
                category_budget_amount_changes,
                category_budgets,
                credit_card_purchases,
                credit_cards,
                direct_expenses,
                incomes,
                accounts
            RESTART IDENTITY CASCADE;
            DELETE FROM categories WHERE owner_user_id IS NOT NULL;
            DELETE FROM users;
            """);
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }

        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
