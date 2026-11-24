using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;

namespace Hearth.Infrastructure.Persistence;

public sealed class HearthDbContext : DbContext
{
    public HearthDbContext(DbContextOptions<HearthDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<PurchaseCondition> PurchaseConditions => Set<PurchaseCondition>();

    public DbSet<CategoryBudget> CategoryBudgets => Set<CategoryBudget>();

    public DbSet<DirectExpense> DirectExpenses => Set<DirectExpense>();

    public DbSet<FixedExpense> FixedExpenses => Set<FixedExpense>();

    public DbSet<CreditCard> CreditCards => Set<CreditCard>();

    public DbSet<CreditCardPurchase> CreditCardPurchases => Set<CreditCardPurchase>();

    public DbSet<Income> Incomes => Set<Income>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Money>()
            .HaveConversion<MoneyConverter>()
            .HavePrecision(18, 2);

        configurationBuilder.Properties<YearMonth>()
            .HaveConversion<YearMonthConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HearthDbContext).Assembly);
    }
}
