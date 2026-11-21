using Hearth.Application.Abstractions;
using Hearth.Application.Accounts.CreateAccount;
using Hearth.Application.Categories;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hearth.Tests.Infrastructure.Persistence;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class AccountAndCategoryPersistenceTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public AccountAndCategoryPersistenceTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AccountRepository_PersistsUpdatesAndReadsBackAllFields()
    {
        var accountId = Guid.CreateVersion7();

        await using (var scope = _fixture.CreateScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var account = Account.Create(
                accountId,
                "Conta principal",
                Money.FromDecimal(-125.45m),
                new DateOnly(2026, 10, 1),
                CardBillingRule.Of(3, 12));

            await accounts.AddAsync(account);
            await uow.SaveChangesAsync();
        }

        await using (var scope = _fixture.CreateScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var loaded = await accounts.GetByIdAsync(accountId);
            Assert.NotNull(loaded);
            Assert.Equal("Conta principal", loaded.Name);
            Assert.Equal(Money.FromDecimal(-125.45m), loaded.InitialBalance);
            Assert.Equal(new DateOnly(2026, 10, 1), loaded.InitialBalanceDate);
            Assert.Equal(CardBillingRule.Of(3, 12), loaded.BillingRule);

            loaded.Rename("Nubank");
            loaded.UpdateInitialBalance(Money.FromDecimal(1500.00m));
            loaded.ChangeBillingRule(CardBillingRule.Of(10, 20));

            await accounts.UpdateAsync(loaded);
            await uow.SaveChangesAsync();
        }

        await using (var scope = _fixture.CreateScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var all = await accounts.GetAllAsync();

            var reloaded = Assert.Single(all);
            Assert.Equal("Nubank", reloaded.Name);
            Assert.Equal(Money.FromDecimal(1500.00m), reloaded.InitialBalance);
            Assert.Equal(new DateOnly(2026, 10, 1), reloaded.InitialBalanceDate);
            Assert.Equal(CardBillingRule.Of(10, 20), reloaded.BillingRule);
        }
    }

    [Fact]
    public async Task CreateAccountHandler_SeedsEightDefaultCategoriesInSingleTransaction()
    {
        Guid accountId;

        await using (var scope = _fixture.CreateScope())
        {
            var handler = new CreateAccountHandler(
                scope.ServiceProvider.GetRequiredService<IAccountRepository>(),
                scope.ServiceProvider.GetRequiredService<ICategoryRepository>(),
                scope.ServiceProvider.GetRequiredService<IUnitOfWork>());

            accountId = await handler.ExecuteAsync(new CreateAccountCommand(
                "Principal", 500.00m, new DateOnly(2026, 10, 1), 3, 12));
        }

        await using (var scope = _fixture.CreateScope())
        {
            var categories = scope.ServiceProvider.GetRequiredService<ICategoryRepository>();
            var saved = await categories.GetByAccountAsync(accountId);

            Assert.Equal(DefaultCategories.Names.Count, saved.Count);
            Assert.Equal(
                DefaultCategories.Names.OrderBy(n => n),
                saved.Select(c => c.Name).OrderBy(n => n));
        }
    }

    [Fact]
    public async Task CategoryRepository_EnforcesCaseInsensitiveUniqueIndexAndRestrictFk()
    {
        var accountId = Guid.CreateVersion7();
        var categoryId = Guid.CreateVersion7();

        await using (var scope = _fixture.CreateScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var categories = scope.ServiceProvider.GetRequiredService<ICategoryRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await accounts.AddAsync(Account.Create(
                accountId, "Conta", Money.Zero, new DateOnly(2026, 1, 1), CardBillingRule.Of(3, 12)));
            await categories.AddAsync(Category.Create(categoryId, accountId, "Alimentação"));
            await uow.SaveChangesAsync();
        }

        await using (var scope = _fixture.CreateScope())
        {
            var categories = scope.ServiceProvider.GetRequiredService<ICategoryRepository>();

            Assert.True(await categories.ExistsWithNameAsync(accountId, "ALIMENTAÇÃO"));
            Assert.False(await categories.ExistsWithNameAsync(accountId, "ALIMENTAÇÃO", excludeCategoryId: categoryId));
        }

        // Duplicate name differing only by case must be rejected by PostgreSQL unique index.
        await using (var scope = _fixture.CreateScope())
        {
            var categories = scope.ServiceProvider.GetRequiredService<ICategoryRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await categories.AddAsync(Category.Create(Guid.CreateVersion7(), accountId, "alimentação"));
            await Assert.ThrowsAsync<DbUpdateException>(() => uow.SaveChangesAsync());
        }

        // Linking an expense blocks direct deletion via FK ON DELETE RESTRICT.
        await using (var scope = _fixture.CreateScope())
        {
            var directExpenses = scope.ServiceProvider.GetRequiredService<IDirectExpenseRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await directExpenses.AddAsync(DirectExpense.Create(
                Guid.CreateVersion7(), accountId, new DateOnly(2026, 10, 10), Money.FromDecimal(45.00m), "Mercado", categoryId));
            await uow.SaveChangesAsync();
        }

        await using (var scope = _fixture.CreateScope())
        {
            var categories = scope.ServiceProvider.GetRequiredService<ICategoryRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var loaded = await categories.GetByIdAsync(categoryId);
            Assert.NotNull(loaded);

            await categories.DeleteAsync(loaded);
            await Assert.ThrowsAsync<DbUpdateException>(() => uow.SaveChangesAsync());
        }
    }
}
