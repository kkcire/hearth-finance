using Hearth.Application.Abstractions;
using Hearth.Application.Accounts.CreateAccount;
using Hearth.Application.Authorization;
using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
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

    private async Task<Guid> SeedUserAsync(string email = "erick@hearth.app")
    {
        var userId = Guid.CreateVersion7();

        await using var scope = _fixture.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await users.AddAsync(User.Create(
            userId, "Erick", email, "hash-placeholder", new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero)));
        await uow.SaveChangesAsync();

        return userId;
    }

    [Fact]
    public async Task AccountRepository_PersistsUpdatesAndReadsBackAllFields()
    {
        var userId = await SeedUserAsync();
        var otherUserId = await SeedUserAsync("outro@hearth.app");
        var accountId = Guid.CreateVersion7();

        await using (var scope = _fixture.CreateScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var account = Account.Create(
                accountId,
                userId,
                "Conta principal",
                Money.FromDecimal(-125.45m),
                new DateOnly(2026, 10, 1),
                isInitialBalanceConfirmed: false);

            await accounts.AddAsync(account);
            await uow.SaveChangesAsync();
        }

        await using (var scope = _fixture.CreateScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var loaded = await accounts.GetByIdAsync(accountId);
            Assert.NotNull(loaded);
            Assert.Equal(userId, loaded.OwnerUserId);
            Assert.Equal("Conta principal", loaded.Name);
            Assert.Equal(Money.FromDecimal(-125.45m), loaded.InitialBalance);
            Assert.Equal(new DateOnly(2026, 10, 1), loaded.InitialBalanceDate);
            Assert.False(loaded.IsInitialBalanceConfirmed);

            loaded.ConfirmInitialBalance(
                Money.FromDecimal(1200.00m),
                new DateOnly(2026, 10, 5),
                new DateOnly(2026, 10, 10));
            loaded.Rename("Nubank");
            loaded.UpdateInitialBalance(Money.FromDecimal(1500.00m));

            await accounts.UpdateAsync(loaded);
            await uow.SaveChangesAsync();
        }

        await using (var scope = _fixture.CreateScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var owned = await accounts.GetByOwnerAsync(userId);
            var others = await accounts.GetByOwnerAsync(otherUserId);

            Assert.Empty(others);
            var reloaded = Assert.Single(owned);
            Assert.Equal("Nubank", reloaded.Name);
            Assert.Equal(Money.FromDecimal(1500.00m), reloaded.InitialBalance);
            Assert.Equal(new DateOnly(2026, 10, 5), reloaded.InitialBalanceDate);
            Assert.True(reloaded.IsInitialBalanceConfirmed);
        }
    }

    [Fact]
    public async Task Migrations_SeedEightGlobalSystemCategoriesWithFixedIds()
    {
        var userId = await SeedUserAsync();

        await using var scope = _fixture.CreateScope();
        var categories = scope.ServiceProvider.GetRequiredService<ICategoryRepository>();
        var visible = await categories.GetVisibleForUserAsync(userId);

        Assert.Equal(SystemCategories.Definitions.Count, visible.Count);
        Assert.All(visible, c => Assert.True(c.IsSystem));
        Assert.Equal(
            SystemCategories.Definitions.Select(s => (s.Id, s.Name)).OrderBy(x => x.Name),
            visible.Select(c => (c.Id, c.Name)).OrderBy(x => x.Name));
    }

    [Fact]
    public async Task CreateAccountHandler_PersistsAccountWithoutDuplicatingSystemCategories()
    {
        var userId = await SeedUserAsync();
        Guid accountId;

        await using (var scope = _fixture.CreateScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var guard = new AccountAccessGuard(accounts, new FakeCurrentUser(userId));
            var handler = new CreateAccountHandler(
                guard,
                accounts,
                scope.ServiceProvider.GetRequiredService<IUnitOfWork>());

            accountId = await handler.ExecuteAsync(new CreateAccountCommand(
                "Principal", 500.00m, new DateOnly(2026, 10, 1)));
        }

        await using (var scope = _fixture.CreateScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var categories = scope.ServiceProvider.GetRequiredService<ICategoryRepository>();

            var savedAccount = await accounts.GetByIdAsync(accountId);
            Assert.NotNull(savedAccount);
            Assert.True(savedAccount.IsInitialBalanceConfirmed);

            var visible = await categories.GetVisibleForUserAsync(userId);
            Assert.Equal(SystemCategories.Definitions.Count, visible.Count);
        }
    }

    [Fact]
    public async Task CategoryRepository_EnforcesCaseInsensitiveUniquenessAgainstSystemAndUserCategoriesAndRestrictFk()
    {
        var userId = await SeedUserAsync();
        var otherUserId = await SeedUserAsync("outro2@hearth.app");
        var accountId = Guid.CreateVersion7();
        var categoryId = Guid.CreateVersion7();

        await using (var scope = _fixture.CreateScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var categories = scope.ServiceProvider.GetRequiredService<ICategoryRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await accounts.AddAsync(Account.Create(
                accountId, userId, "Conta", Money.Zero, new DateOnly(2026, 1, 1)));
            await categories.AddAsync(Category.CreateForUser(categoryId, userId, "Faculdade"));
            await uow.SaveChangesAsync();
        }

        await using (var scope = _fixture.CreateScope())
        {
            var categories = scope.ServiceProvider.GetRequiredService<ICategoryRepository>();

            Assert.True(await categories.ExistsWithNameForUserAsync(userId, "FACULDADE"));
            Assert.False(await categories.ExistsWithNameForUserAsync(userId, "FACULDADE", excludeCategoryId: categoryId));
            // Must also detect case-insensitive collisions against global system categories!
            Assert.True(await categories.ExistsWithNameForUserAsync(userId, "transporte"));
            Assert.True(await categories.ExistsWithNameForUserAsync(userId, "ALIMENTAÇÃO"));
            // Another user can reuse a custom category name from the first user.
            Assert.False(await categories.ExistsWithNameForUserAsync(otherUserId, "Faculdade"));
        }

        // Duplicate custom category name differing only by case for same user must be rejected by PostgreSQL unique index.
        await using (var scope = _fixture.CreateScope())
        {
            var categories = scope.ServiceProvider.GetRequiredService<ICategoryRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await categories.AddAsync(Category.CreateForUser(Guid.CreateVersion7(), userId, "faculdade"));
            await Assert.ThrowsAsync<DbUpdateException>(() => uow.SaveChangesAsync());
        }

        // Linking an expense blocks direct deletion via FK ON DELETE RESTRICT.
        await using (var scope = _fixture.CreateScope())
        {
            var directExpenses = scope.ServiceProvider.GetRequiredService<IDirectExpenseRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await directExpenses.AddAsync(DirectExpense.Create(
                Guid.CreateVersion7(), accountId, new DateOnly(2026, 10, 10), Money.FromDecimal(45.00m), "Mensalidade", categoryId));
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
