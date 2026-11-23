using Hearth.Application.Abstractions;
using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hearth.Tests.Infrastructure.Persistence;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class CreditCardPersistenceTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public CreditCardPersistenceTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> SeedAccountAsync()
    {
        var userId = Guid.CreateVersion7();
        var accountId = Guid.CreateVersion7();

        await using var scope = _fixture.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await users.AddAsync(User.Create(
            userId, "Erick", $"erick-{userId:N}@hearth.app", "hash", new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero)));
        await accounts.AddAsync(Account.Create(
            accountId, userId, "Principal", Money.Zero, new DateOnly(2026, 1, 1), isInitialBalanceConfirmed: true));
        await uow.SaveChangesAsync();

        return accountId;
    }

    [Fact]
    public async Task CreditCardRepository_EnforcesSingleActiveCardPartialUniqueIndex_AndAllowsNewAfterArchive()
    {
        var accountId = await SeedAccountAsync();
        var firstCardId = Guid.CreateVersion7();

        await using (var scope = _fixture.CreateScope())
        {
            var cards = scope.ServiceProvider.GetRequiredService<ICreditCardRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await cards.AddAsync(CreditCard.Create(firstCardId, accountId, CardBillingRule.Of(3, 12), "Nubank", Money.FromDecimal(5000m)));
            await uow.SaveChangesAsync();
        }

        // Second active card must fail via partial unique index ux_credit_cards_account_id_active
        await using (var scope = _fixture.CreateScope())
        {
            var cards = scope.ServiceProvider.GetRequiredService<ICreditCardRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await cards.AddAsync(CreditCard.Create(Guid.CreateVersion7(), accountId, CardBillingRule.Of(10, 20), "Inter", null));
            await Assert.ThrowsAsync<DbUpdateException>(() => uow.SaveChangesAsync());
        }

        // Archiving first card frees the partial unique index slot
        await using (var scope = _fixture.CreateScope())
        {
            var cards = scope.ServiceProvider.GetRequiredService<ICreditCardRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var first = await cards.GetByIdAsync(firstCardId);
            Assert.NotNull(first);
            first.Archive();
            await cards.UpdateAsync(first);
            await uow.SaveChangesAsync();
        }

        var secondCardId = Guid.CreateVersion7();
        await using (var scope = _fixture.CreateScope())
        {
            var cards = scope.ServiceProvider.GetRequiredService<ICreditCardRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await cards.AddAsync(CreditCard.Create(secondCardId, accountId, CardBillingRule.Of(10, 20), "Inter", Money.FromDecimal(3000m)));
            await uow.SaveChangesAsync();
        }

        await using (var scope = _fixture.CreateScope())
        {
            var cards = scope.ServiceProvider.GetRequiredService<ICreditCardRepository>();

            var active = await cards.GetActiveByAccountAsync(accountId);
            Assert.NotNull(active);
            Assert.Equal(secondCardId, active.Id);
            Assert.Equal(2, (await cards.GetByAccountAsync(accountId)).Count);
        }
    }

    [Fact]
    public async Task CreditCardPurchase_SnapshotsBillingRuleAndBlocksCardDeleteViaRestrictFk()
    {
        var accountId = await SeedAccountAsync();
        var cardId = Guid.CreateVersion7();
        var purchaseId = Guid.CreateVersion7();

        await using (var scope = _fixture.CreateScope())
        {
            var cards = scope.ServiceProvider.GetRequiredService<ICreditCardRepository>();
            var purchases = scope.ServiceProvider.GetRequiredService<ICreditCardPurchaseRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var card = CreditCard.Create(cardId, accountId, CardBillingRule.Of(3, 12), "Nubank", Money.FromDecimal(4000m));
            await cards.AddAsync(card);
            await purchases.AddAsync(CreditCardPurchase.Create(
                purchaseId, accountId, cardId, new DateOnly(2026, 10, 10), Money.FromDecimal(200m), 2, 1,
                card.BillingRule, PurchaseCondition.Own, "Monitor", SystemCategories.ShoppingId));
            await uow.SaveChangesAsync();
        }

        // Editing card billing rule must NOT change already persisted purchase rule snapshot
        await using (var scope = _fixture.CreateScope())
        {
            var cards = scope.ServiceProvider.GetRequiredService<ICreditCardRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var card = await cards.GetByIdAsync(cardId);
            card!.UpdateDetails(CardBillingRule.Of(15, 25), "Nubank", Money.FromDecimal(6000m));
            await cards.UpdateAsync(card);
            await uow.SaveChangesAsync();
        }

        await using (var scope = _fixture.CreateScope())
        {
            var purchases = scope.ServiceProvider.GetRequiredService<ICreditCardPurchaseRepository>();

            var savedPurchase = await purchases.GetByIdAsync(purchaseId);
            Assert.NotNull(savedPurchase);
            Assert.Equal(CardBillingRule.Of(3, 12), savedPurchase.Rule);
            Assert.True(await purchases.AnyByCreditCardAsync(cardId));
        }

        // Fresh scope so PostgreSQL FK ON DELETE RESTRICT enforces the delete block
        await using (var scope = _fixture.CreateScope())
        {
            var cards = scope.ServiceProvider.GetRequiredService<ICreditCardRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var card = await cards.GetByIdAsync(cardId);
            await cards.DeleteAsync(card!);
            await Assert.ThrowsAsync<DbUpdateException>(() => uow.SaveChangesAsync());
        }
    }
}
