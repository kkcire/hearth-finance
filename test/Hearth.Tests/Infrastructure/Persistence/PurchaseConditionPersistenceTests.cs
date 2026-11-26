using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.PurchaseConditions.CreatePurchaseCondition;
using Hearth.Application.PurchaseConditions.ListPurchaseConditions;
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
public sealed class PurchaseConditionPersistenceTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public PurchaseConditionPersistenceTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> SeedUserAsync(string email)
    {
        var userId = Guid.CreateVersion7();

        await using var scope = _fixture.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await users.AddAsync(User.Create(
            userId, "Erick", email, "hash", new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero)));
        await uow.SaveChangesAsync();

        return userId;
    }

    [Fact]
    public async Task Migrations_SeedSystemOwnCondition_AndCreateHandlerReusesReactivatesAndIsolatesUsers()
    {
        var userA = await SeedUserAsync("cond-a@hearth.app");
        var userB = await SeedUserAsync("cond-b@hearth.app");

        // 1. Seeded global "Própria" and "Outros" exist and are visible to both users
        await using (var scope = _fixture.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IPurchaseConditionRepository>();
            var visible = await repo.GetVisibleForUserAsync(userA);
            Assert.Equal(2, visible.Count);
            Assert.Contains(visible, c => c.Id == SystemPurchaseConditions.OwnId && c.Name == "Própria" && c.IsSystem);
            Assert.Contains(visible, c => c.Id == SystemPurchaseConditions.OthersId && c.Name == "Outros" && c.IsSystem);
        }

        // 2. Create custom condition for User A, verify case-insensitive idempotence & global match
        Guid momId;
        await using (var scope = _fixture.CreateScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var repo = scope.ServiceProvider.GetRequiredService<IPurchaseConditionRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var guard = new AccountAccessGuard(accounts, new FakeCurrentUser(userA));
            var createHandler = new CreatePurchaseConditionHandler(guard, repo, uow);

            var created = await createHandler.ExecuteAsync(new CreatePurchaseConditionCommand("  Mãe  "));
            momId = created.Id;
            Assert.Equal("Mãe", created.Name);

            var reused = await createHandler.ExecuteAsync(new CreatePurchaseConditionCommand("MÃE"));
            Assert.Equal(momId, reused.Id);

            var systemReused = await createHandler.ExecuteAsync(new CreatePurchaseConditionCommand("própria"));
            Assert.Equal(SystemPurchaseConditions.OwnId, systemReused.Id);

            var othersReused = await createHandler.ExecuteAsync(new CreatePurchaseConditionCommand("OUTROS"));
            Assert.Equal(SystemPurchaseConditions.OthersId, othersReused.Id);
        }

        // 3. Archive User A's condition directly, then CreatePurchaseConditionHandler reactivates it!
        await using (var scope = _fixture.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IPurchaseConditionRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var loaded = await repo.GetByIdAsync(momId);
            Assert.NotNull(loaded);
            loaded.Archive();
            await repo.UpdateAsync(loaded);
            await uow.SaveChangesAsync();
        }

        await using (var scope = _fixture.CreateScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var repo = scope.ServiceProvider.GetRequiredService<IPurchaseConditionRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var guard = new AccountAccessGuard(accounts, new FakeCurrentUser(userA));

            var reactivated = await new CreatePurchaseConditionHandler(guard, repo, uow)
                .ExecuteAsync(new CreatePurchaseConditionCommand("mãe"));
            Assert.Equal(momId, reactivated.Id);
            Assert.False(reactivated.IsArchived);
        }

        // 4. User B does not see User A's condition and can create their own "Mãe"
        await using (var scope = _fixture.CreateScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var repo = scope.ServiceProvider.GetRequiredService<IPurchaseConditionRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var guardB = new AccountAccessGuard(accounts, new FakeCurrentUser(userB));

            var listB = await new ListPurchaseConditionsHandler(guardB, repo).ExecuteAsync();
            Assert.Equal(2, listB.Count);
            Assert.Equal(SystemPurchaseConditions.OwnId, listB[0].Id);
            Assert.Equal(SystemPurchaseConditions.OthersId, listB[1].Id);

            var createdB = await new CreatePurchaseConditionHandler(guardB, repo, uow)
                .ExecuteAsync(new CreatePurchaseConditionCommand("Mãe"));
            Assert.NotEqual(momId, createdB.Id);
        }

        // 5. Direct duplicate insert for same user is rejected by PostgreSQL unique index
        await using (var scope = _fixture.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IPurchaseConditionRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await repo.AddAsync(PurchaseCondition.CreateForUser(Guid.CreateVersion7(), userA, "MÃE"));
            await Assert.ThrowsAsync<DbUpdateException>(() => uow.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task CreditCardPurchase_ConditionFk_EnforcesRestrictOnDelete()
    {
        var userId = await SeedUserAsync("cond-fk@hearth.app");
        var accountId = Guid.CreateVersion7();
        var cardId = Guid.CreateVersion7();
        var conditionId = Guid.CreateVersion7();

        await using (var scope = _fixture.CreateScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var cards = scope.ServiceProvider.GetRequiredService<ICreditCardRepository>();
            var conditions = scope.ServiceProvider.GetRequiredService<IPurchaseConditionRepository>();
            var purchases = scope.ServiceProvider.GetRequiredService<ICreditCardPurchaseRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await accounts.AddAsync(Account.Create(
                accountId, userId, "Conta", Money.Zero, TestTime.UtcOn(new DateOnly(2026, 1, 1)), isInitialBalanceConfirmed: true));
            await cards.AddAsync(CreditCard.Create(
                cardId, accountId, CardBillingRule.Of(3, 12), "Nubank", Money.FromDecimal(3000m)));
            await conditions.AddAsync(PurchaseCondition.CreateForUser(conditionId, userId, "Trabalho"));
            await purchases.AddAsync(CreditCardPurchase.Create(
                Guid.CreateVersion7(), accountId, cardId, new DateOnly(2026, 11, 10), Money.FromDecimal(120m),
                1, 1, CardBillingRule.Of(3, 12), conditionId, "Uber", SystemCategories.TransportId));
            await uow.SaveChangesAsync();
        }

        await using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Hearth.Infrastructure.Persistence.HearthDbContext>();
            var loaded = await db.PurchaseConditions.FirstAsync(c => c.Id == conditionId);
            db.PurchaseConditions.Remove(loaded);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }
}
