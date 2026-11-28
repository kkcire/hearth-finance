using Hearth.Application.Abstractions;
using Hearth.Infrastructure.Persistence;
using Hearth.Infrastructure.Persistence.Repositories;
using Hearth.Infrastructure.Security;
using Hearth.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hearth.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        string timeZoneId = SystemClock.DefaultTimeZoneId)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        services.AddDbContext<HearthDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IPurchaseConditionRepository, PurchaseConditionRepository>();
        services.AddScoped<ICategoryBudgetRepository, CategoryBudgetRepository>();
        services.AddScoped<IDirectExpenseRepository, DirectExpenseRepository>();
        services.AddScoped<IFixedExpenseRepository, FixedExpenseRepository>();
        services.AddScoped<ICreditCardRepository, CreditCardRepository>();
        services.AddScoped<ICreditCardPurchaseRepository, CreditCardPurchaseRepository>();
        services.AddScoped<IIncomeRepository, IncomeRepository>();
        services.AddScoped<IFixedIncomeRepository, FixedIncomeRepository>();

        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock>(provider =>
            new SystemClock(provider.GetRequiredService<TimeProvider>(), timeZoneId));

        return services;
    }
}
