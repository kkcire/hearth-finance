using Hearth.Application.Abstractions;
using Hearth.Infrastructure;
using Hearth.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hearth.Tests.Infrastructure;

public class DependencyInjectionTests
{
    private const string DummyConnectionString =
        "Host=localhost;Port=5432;Database=hearth;Username=hearth;Password=hearth";

    [Fact]
    public void AddInfrastructure_RegistersAllPortsWithExpectedLifetimes()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(DummyConnectionString);

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.Equal(ServiceLifetime.Singleton, services.Single(s => s.ServiceType == typeof(IClock)).Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, services.Single(s => s.ServiceType == typeof(IPasswordHasher)).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, services.Single(s => s.ServiceType == typeof(HearthDbContext)).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, services.Single(s => s.ServiceType == typeof(IUnitOfWork)).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, services.Single(s => s.ServiceType == typeof(IUserRepository)).Lifetime);

        using var scope = provider.CreateScope();
        var scoped = scope.ServiceProvider;

        Assert.NotNull(scoped.GetRequiredService<HearthDbContext>());
        Assert.NotNull(scoped.GetRequiredService<IUnitOfWork>());
        Assert.NotNull(scoped.GetRequiredService<IClock>());
        Assert.NotNull(scoped.GetRequiredService<IPasswordHasher>());
        Assert.NotNull(scoped.GetRequiredService<IUserRepository>());
        Assert.NotNull(scoped.GetRequiredService<IAccountRepository>());
        Assert.NotNull(scoped.GetRequiredService<ICategoryRepository>());
        Assert.NotNull(scoped.GetRequiredService<ICategoryBudgetRepository>());
        Assert.NotNull(scoped.GetRequiredService<IDirectExpenseRepository>());
        Assert.NotNull(scoped.GetRequiredService<IFixedExpenseRepository>());
        Assert.NotNull(scoped.GetRequiredService<ICreditCardPurchaseRepository>());
        Assert.NotNull(scoped.GetRequiredService<IIncomeRepository>());
        Assert.NotNull(scoped.GetRequiredService<IInvoiceRepository>());
    }
}
