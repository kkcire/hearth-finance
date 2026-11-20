using Hearth.Application.Abstractions;
using Hearth.Application.Reports.GetBudgetStatus;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
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

    private async Task<(Guid AccountId, Guid CategoryId)> SeedAccountAndCategoryAsync()
    {
        var accountId = Guid.CreateVersion7();
        var categoryId = Guid.CreateVersion7();

        await using var scope = _fixture.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var categories = scope.ServiceProvider.GetRequiredService<ICategoryRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await accounts.AddAsync(Account.Create(
            accountId, "Conta", Money.FromDecimal(1000.00m), new DateOnly(2026, 1, 1), CardBillingRule.Of(3, 12)));
        await categories.AddAsync(Category.Create(categoryId, accountId, "Transporte"));
        await uow.SaveChangesAsync();

        return (accountId, categoryId);
    }

    [Fact]
    public async Task InvoiceRepository_QueriesByClosingMonthAndEnforcesUniqueAccountClosingMonth()
    {
        var (accountId, _) = await SeedAccountAndCategoryAsync();
        var cycle = BillingCycle.Closing(YearMonth.Of(2026, 11), CardBillingRule.Of(3, 12));

        await using (var scope = _fixture.CreateScope())
        {
            var invoices = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var invoice = Invoice.Create(Guid.CreateVersion7(), accountId, cycle);
            invoice.InformChargedAmount(Money.FromDecimal(450.75m));
            invoice.MarkAsPaid(new DateOnly(2026, 11, 12));

            await invoices.AddAsync(invoice);
            await uow.SaveChangesAsync();
        }

        await using (var scope = _fixture.CreateScope())
        {
            var invoices = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();

            var found = await invoices.GetByAccountAndClosingMonthAsync(accountId, YearMonth.Of(2026, 11));
            Assert.NotNull(found);
            Assert.Equal(cycle, found.Cycle);
            Assert.Equal(Money.FromDecimal(450.75m), found.ChargedAmount);
            Assert.Equal(new DateOnly(2026, 11, 12), found.PaidOn);
            Assert.Null(await invoices.GetByAccountAndClosingMonthAsync(accountId, YearMonth.Of(2026, 12)));
        }

        // Second invoice for same (AccountId, ClosingMonth) must be rejected by PostgreSQL unique index.
        await using (var scope = _fixture.CreateScope())
        {
            var invoices = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await invoices.AddAsync(Invoice.Create(Guid.CreateVersion7(), accountId, cycle));
            await Assert.ThrowsAsync<DbUpdateException>(() => uow.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task CategoryBudgetRepository_PersistsAmountHistoryUpdatesAndIntegratesWithGetBudgetStatus()
    {
        var (accountId, categoryId) = await SeedAccountAndCategoryAsync();
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
            var handler = new GetBudgetStatusHandler(
                scope.ServiceProvider.GetRequiredService<IAccountRepository>(),
                scope.ServiceProvider.GetRequiredService<ICategoryRepository>(),
                scope.ServiceProvider.GetRequiredService<ICategoryBudgetRepository>(),
                scope.ServiceProvider.GetRequiredService<IDirectExpenseRepository>(),
                scope.ServiceProvider.GetRequiredService<IFixedExpenseRepository>(),
                scope.ServiceProvider.GetRequiredService<ICreditCardPurchaseRepository>());

            var status = await handler.ExecuteAsync(new GetBudgetStatusQuery(accountId, 2026, 11));

            var item = Assert.Single(status.Items);
            Assert.Equal("Transporte", item.CategoryName);
            Assert.Equal(200.00m, item.BudgetAmount);
            Assert.Equal(150.00m, item.SpentAmount);
            Assert.Equal(50.00m, item.RemainingAmount);
            Assert.Equal(75.00m, item.UsagePercentage);
            Assert.False(item.IsExceeded);
        }
    }
}
