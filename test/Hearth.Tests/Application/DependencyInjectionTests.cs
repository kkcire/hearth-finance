using Hearth.Application;
using Hearth.Application.Abstractions;
using Hearth.Tests.Application.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace Hearth.Tests.Application;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(() => services.AddApplication());
    }

    [Fact]
    public void AddApplication_RegistersEveryHandlerAsScopedAndResolvable()
    {
        var services = new ServiceCollection();
        RegisterFakes(services);
        services.AddApplication();

        var handlerTypes = typeof(DependencyInjection).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsPublic: true }
                && type.Name.EndsWith("Handler", StringComparison.Ordinal))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(handlerTypes);

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        foreach (var handlerType in handlerTypes)
        {
            var descriptor = Assert.Single(services, item => item.ServiceType == handlerType);
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);

            var instance = scope.ServiceProvider.GetRequiredService(handlerType);
            Assert.IsType(handlerType, instance);
        }
    }

    private static void RegisterFakes(IServiceCollection services)
    {
        var users = new InMemoryUserRepository();
        var accounts = new InMemoryAccountRepository();
        var categories = new InMemoryCategoryRepository();
        var budgets = new InMemoryCategoryBudgetRepository();
        var directExpenses = new InMemoryDirectExpenseRepository();
        var fixedExpenses = new InMemoryFixedExpenseRepository();
        var purchases = new InMemoryCreditCardPurchaseRepository();
        var incomes = new InMemoryIncomeRepository();
        var invoices = new InMemoryInvoiceRepository();

        services.AddSingleton<IUserRepository>(users);
        services.AddSingleton<IPasswordHasher>(new FakePasswordHasher());
        services.AddSingleton<IAccountRepository>(accounts);
        services.AddSingleton<ICategoryRepository>(categories);
        services.AddSingleton<ICategoryBudgetRepository>(budgets);
        services.AddSingleton<IDirectExpenseRepository>(directExpenses);
        services.AddSingleton<IFixedExpenseRepository>(fixedExpenses);
        services.AddSingleton<ICreditCardPurchaseRepository>(purchases);
        services.AddSingleton<IIncomeRepository>(incomes);
        services.AddSingleton<IInvoiceRepository>(invoices);
        services.AddSingleton<IUnitOfWork>(new FakeUnitOfWork());
        services.AddSingleton<IClock>(new FakeClock(new DateOnly(2026, 11, 20)));
    }
}
