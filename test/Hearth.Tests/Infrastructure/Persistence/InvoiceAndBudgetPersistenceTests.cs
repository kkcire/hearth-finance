using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Invoices.GetInvoiceSummary;
using Hearth.Application.Reports.GetMonthlyDashboard;
using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hearth.Tests.Infrastructure.Persistence;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class InvoiceAndBudgetPersistenceTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public InvoiceAndBudgetPersistenceTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(Guid UserId, Guid AccountId)> SeedUserAndAccountAsync()
    {
        var userId = Guid.CreateVersion7();
        var accountId = Guid.CreateVersion7();

        await using var scope = _fixture.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await users.AddAsync(User.Create(
            userId, "Erick", $"erick-{userId:N}@hearth.app", "hash-placeholder", new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero)));
        await accounts.AddAsync(Account.Create(
            accountId, userId, "Conta", Money.FromDecimal(1000.00m), TestTime.UtcOn(new DateOnly(2026, 1, 1))));
        await uow.SaveChangesAsync();

        return (userId, accountId);
    }

    [Fact]
    public async Task GetInvoiceSummaryHandler_DerivesInvoiceDirectlyFromPersistedCardsAndPurchases()
    {
        var (userId, accountId) = await SeedUserAndAccountAsync();
        var cardId = Guid.CreateVersion7();
        var rule = CardBillingRule.Of(3, 12);

        await using (var scope = _fixture.CreateScope())
        {
            var cards = scope.ServiceProvider.GetRequiredService<ICreditCardRepository>();
            var purchases = scope.ServiceProvider.GetRequiredService<ICreditCardPurchaseRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await cards.AddAsync(CreditCard.Create(cardId, accountId, rule, "Nubank", Money.FromDecimal(3000.00m)));
            await purchases.AddAsync(CreditCardPurchase.Create(
                Guid.CreateVersion7(),
                accountId,
                cardId,
                new DateOnly(2026, 10, 10),
                Money.FromDecimal(300.00m),
                3,
                1,
                rule,
                SystemPurchaseConditions.OwnId,
                "Passagem",
                SystemCategories.TransportId));
            await uow.SaveChangesAsync();
        }

        await using (var scope = _fixture.CreateScope())
        {
            var guard = new AccountAccessGuard(
                scope.ServiceProvider.GetRequiredService<IAccountRepository>(),
                new FakeCurrentUser(userId));
            var handler = new GetInvoiceSummaryHandler(
                guard,
                scope.ServiceProvider.GetRequiredService<ICreditCardRepository>(),
                scope.ServiceProvider.GetRequiredService<ICreditCardPurchaseRepository>(),
                new FakeClock(new DateOnly(2026, 11, 15)));

            var summary = await handler.ExecuteAsync(new GetInvoiceSummaryQuery(accountId, 2026, 11));

            Assert.True(summary.HasActiveCard);
            Assert.False(summary.IsEmptyWithoutCard);
            Assert.Equal(100.00m, summary.CalculatedAmount);
            Assert.Equal(InvoiceStatus.Paid, summary.Status);
            Assert.Equal(new DateOnly(2026, 11, 12), summary.DueDate);
            Assert.Single(summary.Installments);
        }
    }

    [Fact]
    public async Task CategoryBudgetRepository_PersistsAmountHistoryUpdatesAndIntegratesWithGetBudgetStatus()
    {
        var (userId, accountId) = await SeedUserAndAccountAsync();
        var categoryId = SystemCategories.TransportId;
        var budgetId = Guid.CreateVersion7();

        await using (var scope = _fixture.CreateScope())
        {
            var budgets = scope.ServiceProvider.GetRequiredService<ICategoryBudgetRepository>();
            var directs = scope.ServiceProvider.GetRequiredService<IDirectExpenseRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var budget = CategoryBudget.Create(
                budgetId, accountId, categoryId, YearMonth.Of(2026, 1), null, Money.FromDecimal(160.00m));
            await budgets.AddAsync(budget);

            await directs.AddAsync(DirectExpense.Create(
                Guid.CreateVersion7(), accountId, new DateOnly(2026, 11, 10), Money.FromDecimal(150.00m), "Ônibus", categoryId));

            await uow.SaveChangesAsync();
        }

        await using (var scope = _fixture.CreateScope())
        {
            var budgets = scope.ServiceProvider.GetRequiredService<ICategoryBudgetRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var loaded = await budgets.GetByIdAsync(budgetId);
            Assert.NotNull(loaded);

            loaded.ChangeAmount(Money.FromDecimal(200.00m), new DateOnly(2026, 11, 5));
            loaded.End(YearMonth.Of(2026, 12), new DateOnly(2026, 11, 5));

            await budgets.UpdateAsync(loaded);
            await uow.SaveChangesAsync();
        }

        await using (var scope = _fixture.CreateScope())
        {
            var guard = new AccountAccessGuard(
                scope.ServiceProvider.GetRequiredService<IAccountRepository>(),
                new FakeCurrentUser(userId));
            var handler = new GetMonthlyDashboardHandler(
                guard,
                scope.ServiceProvider.GetRequiredService<ICreditCardRepository>(),
                scope.ServiceProvider.GetRequiredService<ICategoryRepository>(),
                scope.ServiceProvider.GetRequiredService<ICategoryBudgetRepository>(),
                scope.ServiceProvider.GetRequiredService<IIncomeRepository>(),
                scope.ServiceProvider.GetRequiredService<IFixedIncomeRepository>(),
                scope.ServiceProvider.GetRequiredService<IDirectExpenseRepository>(),
                scope.ServiceProvider.GetRequiredService<IFixedExpenseRepository>(),
                scope.ServiceProvider.GetRequiredService<ICreditCardPurchaseRepository>(),
                new FakeClock(new DateOnly(2026, 11, 15)));

            var dashboard = await handler.ExecuteAsync(new GetMonthlyDashboardQuery(accountId, YearMonth.Of(2026, 11)));

            var item = Assert.Single(dashboard.CategorySpending);
            Assert.Equal("Transporte", item.CategoryName);
            Assert.Equal(200.00m, item.BudgetAmount);
            Assert.Equal(150.00m, item.AmountSpent);
            Assert.Equal(75.0m, item.BudgetUsagePercentage);
        }
    }
}
