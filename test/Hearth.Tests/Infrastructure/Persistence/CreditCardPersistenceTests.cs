using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.CreditCardPurchases.RegisterCreditCardPurchase;
using Hearth.Application.CreditCards.ArchiveCreditCard;
using Hearth.Application.CreditCards.CreateCreditCard;
using Hearth.Application.CreditCards.DeleteCreditCard;
using Hearth.Application.CreditCards.ListCreditCards;
using Hearth.Application.CreditCards.RestoreCreditCard;
using Hearth.Application.Invoices.GetInvoiceSummary;
using Hearth.Application.Reports.GetMonthlyDashboard;
using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Infrastructure;
using Hearth.Infrastructure.Persistence;
using Hearth.Tests.Application.Fakes;
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
            accountId, userId, "Principal", Money.Zero, TestTime.UtcOn(new DateOnly(2026, 1, 1)), isInitialBalanceConfirmed: true));
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
                card.BillingRule, SystemPurchaseConditions.OwnId, "Monitor", SystemCategories.ShoppingId));
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
                var ownPurchaseId = Guid.CreateVersion7();
                var othersPurchaseId = Guid.CreateVersion7();

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
                        ({ownPurchaseId}, {account1Id}, {oldCustom2}, DATE '2026-10-10', 120.00, 2, 1, 'Livro', 3, 12, 0, NULL),
                        ({othersPurchaseId}, {account1Id}, {oldCustom2}, DATE '2026-10-11', 80.00, 1, 1, 'Presente', 3, 12, 1, NULL);

                    INSERT INTO invoices (id, account_id, closing_month, closing_day, due_day, charged_amount, paid_on)
                    VALUES ({Guid.CreateVersion7()}, {account1Id}, 202611, 3, 12, 60.00, DATE '2026-11-12');
                    """);
            }

            // 2. Apply RedesignMvpSchema and AddPurchaseConditions onto populated database
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

                var purchases = await db.CreditCardPurchases.OrderBy(p => p.Date).ToListAsync();
                Assert.Equal(2, purchases.Count);
                Assert.All(purchases, p =>
                {
                    Assert.Equal(card.Id, p.CreditCardId);
                    Assert.Equal(categories.Single(c => c.Name == "Faculdade").Id, p.CategoryId);
                });
                Assert.Equal(SystemPurchaseConditions.OwnId, purchases[0].ConditionId);
                Assert.Equal(SystemPurchaseConditions.OthersId, purchases[1].ConditionId);

                var conditions = await db.PurchaseConditions.ToListAsync();
                Assert.Equal(SystemPurchaseConditions.Definitions.Count, conditions.Count);
                Assert.Contains(conditions, c => c.Id == SystemPurchaseConditions.OwnId && c.Name == "Própria");
                Assert.Contains(conditions, c => c.Id == SystemPurchaseConditions.OthersId && c.Name == "Outros");
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

    [Fact]
    public async Task RegisterCreditCardPurchaseHandler_OldTenInstallmentPurchaseWithInitialInstallment9_CountsEntryMonthPaidInstallmentAndIgnoresPreTrackingInstallments()
    {
        var userId = Guid.CreateVersion7();
        var accountId = Guid.CreateVersion7();
        var cardId = Guid.CreateVersion7();
        var initialBalanceDate = new DateOnly(2026, 11, 22);
        var today = new DateOnly(2026, 11, 22);

        await using (var scope = _fixture.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var cards = scope.ServiceProvider.GetRequiredService<ICreditCardRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await users.AddAsync(User.Create(
                userId, "Erick", "installment9@hearth.app", "hash", new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero)));
            await accounts.AddAsync(Account.Create(
                accountId, userId, "Conta principal", Money.FromDecimal(2000.00m), TestTime.UtcOn(initialBalanceDate), isInitialBalanceConfirmed: true));
            await cards.AddAsync(CreditCard.Create(
                cardId, accountId, CardBillingRule.Of(2, 12), "Nubank", Money.FromDecimal(3000.00m)));
            await uow.SaveChangesAsync();
        }

        // Register:
        // 1) 10x purchase from 02/03/2026 with InitialInstallment = 9:
        //    inst 1..7 due Apr..Oct 2026 (< TrackingStartDate 2026-11-01 -> excluded),
        //    inst 8 due Nov 12, 2026 (>= TrackingStartDate 2026-11-01 -> INCLUDED in entry-month paid invoice & calculations!),
        //    inst 9 due Dec 12, inst 10 due Jan 12 (>= TrackingStartDate 2026-11-01 -> INCLUDED).
        // 2) Purchase from 10/10/2026 of R$ 500 due on 12/11/2026 (>= TrackingStartDate 2026-11-01, settled in starting month Nov 2026!)
        // 3) Fully pre-tracking purchase from 10/09/2026 due on 12/10/2026 (< TrackingStartDate 2026-11-01) -> REJECTED by Domain!
        Guid purchaseId;
        Guid novemberInvoiceId;
        await using (var scope = _fixture.CreateScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var guard = new AccountAccessGuard(accounts, new FakeCurrentUser(userId));
            var handler = new RegisterCreditCardPurchaseHandler(
                guard,
                scope.ServiceProvider.GetRequiredService<ICreditCardRepository>(),
                scope.ServiceProvider.GetRequiredService<ICategoryRepository>(),
                scope.ServiceProvider.GetRequiredService<IPurchaseConditionRepository>(),
                scope.ServiceProvider.GetRequiredService<ICreditCardPurchaseRepository>(),
                scope.ServiceProvider.GetRequiredService<IUnitOfWork>());

            purchaseId = await handler.ExecuteAsync(new RegisterCreditCardPurchaseCommand(
                accountId,
                cardId,
                new DateOnly(2026, 3, 2),
                1000.00m,
                10,
                9,
                SystemPurchaseConditions.OwnId,
                "Geladeira",
                SystemCategories.ShoppingId));

            novemberInvoiceId = await handler.ExecuteAsync(new RegisterCreditCardPurchaseCommand(
                accountId,
                cardId,
                new DateOnly(2026, 10, 10),
                500.00m,
                1,
                1,
                SystemPurchaseConditions.OwnId,
                "Fatura novembro",
                SystemCategories.ShoppingId));

            await Assert.ThrowsAsync<Hearth.Domain.Exceptions.DomainException>(() => handler.ExecuteAsync(
                new RegisterCreditCardPurchaseCommand(
                    accountId,
                    cardId,
                    new DateOnly(2026, 9, 10),
                    250.00m,
                    1,
                    1,
                    SystemPurchaseConditions.OwnId,
                    "Fatura outubro fora do período",
                    SystemCategories.ShoppingId)));
        }

        await using (var scope = _fixture.CreateScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var cards = scope.ServiceProvider.GetRequiredService<ICreditCardRepository>();
            var incomes = scope.ServiceProvider.GetRequiredService<IIncomeRepository>();
            var directs = scope.ServiceProvider.GetRequiredService<IDirectExpenseRepository>();
            var fixedExpenses = scope.ServiceProvider.GetRequiredService<IFixedExpenseRepository>();
            var purchases = scope.ServiceProvider.GetRequiredService<ICreditCardPurchaseRepository>();
            var guard = new AccountAccessGuard(accounts, new FakeCurrentUser(userId));
            var clock = new FakeClock(today);

            var saved = await purchases.GetByIdAsync(purchaseId);
            Assert.NotNull(saved);
            Assert.Equal(9, saved.InitialInstallment);
            Assert.Equal(new DateOnly(2026, 4, 12), saved.FirstCycle.DueDate);
            Assert.Equal(new DateOnly(2026, 12, 12), saved.FirstActiveCycle.DueDate);

            var novPurchase = await purchases.GetByIdAsync(novemberInvoiceId);
            Assert.NotNull(novPurchase);
            Assert.Equal(new DateOnly(2026, 11, 12), novPurchase.FirstActiveCycle.DueDate);

            // Dashboard on 2026-11-22 includes BOTH Nov 12 installments (500 + inst 8 of 100 = 600) in
            // PaidInvoices, CurrentMonthFlow.CardInvoice, TotalExpenses, and CategorySpending —
            // while never including pre-tracking installments 1..7 (Apr..Oct 2026 < 2026-11-01).
            var dashboardHandler = new GetMonthlyDashboardHandler(
                guard,
                cards,
                scope.ServiceProvider.GetRequiredService<ICategoryRepository>(),
                scope.ServiceProvider.GetRequiredService<ICategoryBudgetRepository>(),
                incomes,
                scope.ServiceProvider.GetRequiredService<IFixedIncomeRepository>(),
                directs,
                fixedExpenses,
                purchases,
                clock);
            var dashboard = await dashboardHandler.ExecuteAsync(new GetMonthlyDashboardQuery(accountId, YearMonth.Of(2026, 11)));
            Assert.Equal(600.00m, dashboard.Balance.PaidInvoices);
            Assert.Equal(1400.00m, dashboard.Balance.Amount);
            Assert.Equal(600.00m, dashboard.CurrentMonthFlow.CardInvoice);
            Assert.Equal(600.00m, dashboard.CurrentMonthFlow.TotalExpenses);
            Assert.Equal(600.00m, dashboard.CategorySpending.Sum(c => c.AmountSpent));
            Assert.Equal(YearMonth.Of(2026, 12), dashboard.CurrentInvoice.BillingMonth);
            Assert.Equal(100.00m, dashboard.CurrentInvoice.CycleAmount);
            Assert.Equal(100.00m, dashboard.CurrentInvoice.FutureInstallmentsTotal);

            // Used credit card limit counts ONLY unpaid installments 9 and 10 (R$ 200.00), leaving R$ 2800.00 available
            var activeCards = await new ListCreditCardsHandler(guard, cards, purchases, clock)
                .ExecuteAsync(new ListCreditCardsQuery(accountId));
            var activeCard = Assert.Single(activeCards);
            Assert.Equal(200.00m, activeCard.UsedLimit);
            Assert.Equal(2800.00m, activeCard.AvailableLimit);

            // Invoices: Oct 2026 (pre-tracking) = 0, Nov 2026 = 600 (500 + inst 8), Dec 2026 = 100 (inst 9), Jan 2027 = 100 (inst 10)
            var invoiceHandler = new GetInvoiceSummaryHandler(guard, cards, purchases, clock);
            Assert.Equal(0.00m, (await invoiceHandler.ExecuteAsync(new GetInvoiceSummaryQuery(accountId, 2026, 10))).CalculatedAmount);
            var novInvoice = await invoiceHandler.ExecuteAsync(new GetInvoiceSummaryQuery(accountId, 2026, 11));
            Assert.Equal(600.00m, novInvoice.CalculatedAmount);
            Assert.Equal(2, novInvoice.Installments.Count);
            Assert.Equal(100.00m, (await invoiceHandler.ExecuteAsync(new GetInvoiceSummaryQuery(accountId, 2026, 12))).CalculatedAmount);
            Assert.Equal(100.00m, (await invoiceHandler.ExecuteAsync(new GetInvoiceSummaryQuery(accountId, 2027, 1))).CalculatedAmount);
        }
    }

    [Fact]
    public async Task CreditCardLifecycle_ArchiveRestoreAndSafeDelete_PersistsCleanlyInPostgres()
    {
        var userId = Guid.CreateVersion7();
        var accountId = Guid.CreateVersion7();

        await using (var scope = _fixture.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await users.AddAsync(User.Create(
                userId, "Erick", $"lifecycle-{userId:N}@hearth.app", "hash", new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero)));
            await accounts.AddAsync(Account.Create(
                accountId, userId, "Principal", Money.FromDecimal(1000m), TestTime.UtcOn(new DateOnly(2026, 1, 1)), isInitialBalanceConfirmed: true));
            await uow.SaveChangesAsync();
        }

        Guid cardWithHistoryId;
        Guid unusedCardId;

        await using (var scope = _fixture.CreateScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var cards = scope.ServiceProvider.GetRequiredService<ICreditCardRepository>();
            var categories = scope.ServiceProvider.GetRequiredService<ICategoryRepository>();
            var conditions = scope.ServiceProvider.GetRequiredService<IPurchaseConditionRepository>();
            var purchases = scope.ServiceProvider.GetRequiredService<ICreditCardPurchaseRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var guard = new AccountAccessGuard(accounts, new FakeCurrentUser(userId));

            var create = new CreateCreditCardHandler(guard, cards, uow);
            var archive = new ArchiveCreditCardHandler(guard, cards, uow);
            var restore = new RestoreCreditCardHandler(guard, cards, uow);
            var registerPurchase = new RegisterCreditCardPurchaseHandler(guard, cards, categories, conditions, purchases, uow);

            cardWithHistoryId = await create.ExecuteAsync(new CreateCreditCardCommand(accountId, 3, 12, "Nubank", 5000m));
            await registerPurchase.ExecuteAsync(new RegisterCreditCardPurchaseCommand(
                accountId, cardWithHistoryId, new DateOnly(2026, 11, 10), 200m, 1, 1,
                SystemPurchaseConditions.OwnId, "Fone", SystemCategories.ShoppingId));

            // Archive -> Restore -> Archive again -> Create unused second card -> Archive unused
            Assert.True(await archive.ExecuteAsync(new ArchiveCreditCardCommand(cardWithHistoryId)));
            Assert.True(await restore.ExecuteAsync(new RestoreCreditCardCommand(cardWithHistoryId)));
            Assert.True(await archive.ExecuteAsync(new ArchiveCreditCardCommand(cardWithHistoryId)));

            unusedCardId = await create.ExecuteAsync(new CreateCreditCardCommand(accountId, 10, 20, "Inter", 3000m));

            // Restoring cardWithHistoryId while unusedCardId is active must throw DomainException
            await Assert.ThrowsAsync<DomainException>(() => restore.ExecuteAsync(new RestoreCreditCardCommand(cardWithHistoryId)));

            Assert.True(await archive.ExecuteAsync(new ArchiveCreditCardCommand(unusedCardId)));
        }

        await using (var scope = _fixture.CreateScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var cards = scope.ServiceProvider.GetRequiredService<ICreditCardRepository>();
            var purchases = scope.ServiceProvider.GetRequiredService<ICreditCardPurchaseRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var guard = new AccountAccessGuard(accounts, new FakeCurrentUser(userId));
            var clock = new FakeClock(new DateOnly(2026, 11, 20));

            var delete = new DeleteCreditCardHandler(guard, cards, purchases, uow);
            var restore = new RestoreCreditCardHandler(guard, cards, uow);
            var list = new ListCreditCardsHandler(guard, cards, purchases, clock);

            // Deleting archived card WITH purchases is blocked
            await Assert.ThrowsAsync<DomainException>(() => delete.ExecuteAsync(new DeleteCreditCardCommand(cardWithHistoryId)));

            // Deleting archived card WITHOUT purchases succeeds
            await delete.ExecuteAsync(new DeleteCreditCardCommand(unusedCardId));

            // Restoring cardWithHistoryId succeeds now that no active card exists
            Assert.True(await restore.ExecuteAsync(new RestoreCreditCardCommand(cardWithHistoryId)));

            var activeCards = await list.ExecuteAsync(new ListCreditCardsQuery(accountId, IncludeArchived: false));
            var singleActive = Assert.Single(activeCards);
            Assert.Equal(cardWithHistoryId, singleActive.Id);
            Assert.False(singleActive.IsArchived);
        }
    }
}
