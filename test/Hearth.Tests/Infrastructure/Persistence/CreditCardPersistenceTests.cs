using Hearth.Application.Abstractions;
using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Infrastructure;
using Hearth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
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

    [Fact]
    public async Task RedesignMvpSchemaMigration_UpgradesPopulatedLegacyDatabaseWithoutConstraintErrors()
    {
        var upgradeDbName = $"hearth_upgrade_{Guid.NewGuid():N}";
        var baseCsb = new NpgsqlConnectionStringBuilder(_fixture.ConnectionString);
        var upgradeCsb = new NpgsqlConnectionStringBuilder(_fixture.ConnectionString) { Database = upgradeDbName };

        await using (var adminConn = new NpgsqlConnection(baseCsb.ConnectionString))
        {
            await adminConn.OpenAsync();
            await using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{upgradeDbName}\";", adminConn);
            await createCmd.ExecuteNonQueryAsync();
        }

        try
        {
            var services = new ServiceCollection();
            services.AddInfrastructure(upgradeCsb.ConnectionString);
            await using var provider = services.BuildServiceProvider(validateScopes: true);

            // 1. Migrate to the previous migration (AddAccountOwnerUserId)
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<HearthDbContext>();
                var migrator = db.GetService<IMigrator>();
                await migrator.MigrateAsync("20261122145710_AddAccountOwnerUserId");

                var userId = Guid.CreateVersion7();
                var account1Id = Guid.CreateVersion7();
                var account2Id = Guid.CreateVersion7();
                var oldTransport1 = Guid.CreateVersion7();
                var oldTransport2 = Guid.CreateVersion7();
                var oldCustom1 = Guid.CreateVersion7();
                var oldCustom2 = Guid.CreateVersion7();
                var directId = Guid.CreateVersion7();
                var purchaseId = Guid.CreateVersion7();

                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO users (id, name, email, password_hash, created_at_utc)
                    VALUES ({userId}, 'Erick', 'erick-legacy@hearth.app', 'hash', TIMESTAMPTZ '2026-11-22 12:00:00+00');

                    INSERT INTO accounts (id, owner_user_id, name, initial_balance, initial_balance_date, closing_day, due_day)
                    VALUES
                        ({account1Id}, {userId}, 'Conta 1', 500.00, DATE '2026-01-01', 3, 12),
                        ({account2Id}, {userId}, 'Conta 2', 100.00, DATE '2026-01-01', 10, 20);

                    INSERT INTO categories (id, account_id, name, is_archived)
                    VALUES
                        ({oldTransport1}, {account1Id}, 'Transporte', false),
                        ({oldTransport2}, {account2Id}, 'Transporte', false),
                        ({oldCustom1}, {account1Id}, 'Faculdade', false),
                        ({oldCustom2}, {account2Id}, 'Faculdade', false);

                    INSERT INTO direct_expenses (id, account_id, category_id, date, amount, description)
                    VALUES ({directId}, {account1Id}, {oldTransport1}, DATE '2026-10-10', 25.00, 'Ônibus');

                    INSERT INTO credit_card_purchases
                        (id, account_id, category_id, date, amount, installment_count, initial_installment, description, closing_day, due_day, condition_kind, condition_name)
                    VALUES
                        ({purchaseId}, {account1Id}, {oldCustom2}, DATE '2026-10-10', 120.00, 2, 1, 'Livro', 3, 12, 0, NULL);

                    INSERT INTO invoices (id, account_id, closing_month, closing_day, due_day, charged_amount, paid_on)
                    VALUES ({Guid.CreateVersion7()}, {account1Id}, 202611, 3, 12, 60.00, DATE '2026-11-12');
                    """);
            }

            // 2. Apply RedesignMvpSchema onto populated database
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<HearthDbContext>();
                await db.Database.MigrateAsync();

                var accounts = await db.Accounts.ToListAsync();
                Assert.Equal(2, accounts.Count);
                Assert.All(accounts, a => Assert.True(a.IsInitialBalanceConfirmed));

                var categories = await db.Categories.ToListAsync();
                Assert.Equal(SystemCategories.Definitions.Count + 1, categories.Count); // 8 globals + 1 deduplicated "Faculdade"

                var direct = Assert.Single(await db.DirectExpenses.ToListAsync());
                Assert.Equal(SystemCategories.TransportId, direct.CategoryId);

                var card = Assert.Single(await db.CreditCards.ToListAsync());
                Assert.Equal(CardBillingRule.Of(3, 12), card.BillingRule);

                var purchase = Assert.Single(await db.CreditCardPurchases.ToListAsync());
                Assert.Equal(card.Id, purchase.CreditCardId);
                Assert.Equal(categories.Single(c => c.Name == "Faculdade").Id, purchase.CategoryId);
            }
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var adminConn = new NpgsqlConnection(baseCsb.ConnectionString);
            await adminConn.OpenAsync();
            await using var dropCmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{upgradeDbName}\" WITH (FORCE);", adminConn);
            await dropCmd.ExecuteNonQueryAsync();
        }
    }
}
