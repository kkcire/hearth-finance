using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Reports.GetMonthlySurplus;
using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hearth.Tests.Infrastructure.Persistence;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class MonthlySurplusPersistenceTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public MonthlySurplusPersistenceTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetMonthlySurplusHandler_EndToEndWithPostgres_IncludesArchivedCardInstallmentsAndRollsOverMonths()
    {
        var userId = Guid.CreateVersion7();
        var accountId = Guid.CreateVersion7();
        var activeCardId = Guid.CreateVersion7();
        var archivedCardId = Guid.CreateVersion7();
        var today = new DateOnly(2026, 11, 20);

        await using (var scope = _fixture.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var cards = scope.ServiceProvider.GetRequiredService<ICreditCardRepository>();
            var incomes = scope.ServiceProvider.GetRequiredService<IIncomeRepository>();
            var directs = scope.ServiceProvider.GetRequiredService<IDirectExpenseRepository>();
            var fixedExpenses = scope.ServiceProvider.GetRequiredService<IFixedExpenseRepository>();
            var purchases = scope.ServiceProvider.GetRequiredService<ICreditCardPurchaseRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await users.AddAsync(User.Create(
                userId, "Erick", "surplus@hearth.app", "hash-placeholder", new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero)));

            await accounts.AddAsync(Account.Create(
                accountId, userId, "Conta principal", Money.FromDecimal(2000.00m), new DateOnly(2026, 10, 1), isInitialBalanceConfirmed: true));

            var activeCard = CreditCard.Create(
                activeCardId, accountId, CardBillingRule.Of(25, 5), "Cartão Ativo", Money.FromDecimal(5000.00m));
            var archivedCard = CreditCard.Create(
                archivedCardId, accountId, CardBillingRule.Of(3, 12), "Cartão Antigo", Money.FromDecimal(3000.00m));
            archivedCard.Archive();
            await cards.AddAsync(activeCard);
            await cards.AddAsync(archivedCard);

            // Oct income received Oct 5 (+4000), Nov income pending Nov 25 (+4000)
            var octSalary = Income.Create(
                Guid.CreateVersion7(), accountId, new DateOnly(2026, 10, 5), Money.FromDecimal(4000.00m), "Salário Out");
            octSalary.MarkAsReceived(new DateOnly(2026, 10, 5), today);
            var novSalary = Income.Create(
                Guid.CreateVersion7(), accountId, new DateOnly(2026, 11, 25), Money.FromDecimal(4000.00m), "Salário Nov");
            await incomes.AddAsync(octSalary);
            await incomes.AddAsync(novSalary);

            // Oct direct expense (-500), Nov direct expense settled Nov 10 (-300)
            await directs.AddAsync(DirectExpense.Create(
                Guid.CreateVersion7(), accountId, new DateOnly(2026, 10, 15), Money.FromDecimal(500.00m), "Mercado Out", SystemCategories.FoodId));
            await directs.AddAsync(DirectExpense.Create(
                Guid.CreateVersion7(), accountId, new DateOnly(2026, 11, 10), Money.FromDecimal(300.00m), "Mercado Nov", SystemCategories.FoodId));

            // Fixed expense with history: 1000 in Oct (paid Oct 10), changed to 1200 from Nov (unpaid)
            var rent = FixedExpense.Create(
                Guid.CreateVersion7(), accountId, YearMonth.Of(2026, 10), null, Money.FromDecimal(1000.00m), "Aluguel", SystemCategories.ServicesId);
            rent.MarkAsPaid(YearMonth.Of(2026, 10), new DateOnly(2026, 10, 10), today);
            rent.ChangeAmount(Money.FromDecimal(1200.00m), new DateOnly(2026, 11, 1));
            await fixedExpenses.AddAsync(rent);

            // Active card (25/5): purchase Nov 10 of 600 in 2x -> 300 due Dec 5, 300 due Jan 5
            await purchases.AddAsync(CreditCardPurchase.Create(
                Guid.CreateVersion7(), accountId, activeCardId, new DateOnly(2026, 11, 10), Money.FromDecimal(600.00m),
                2, 1, activeCard.BillingRule, PurchaseCondition.Own, "Eletro", SystemCategories.ShoppingId));

            // Archived card (3/12): purchase Oct 10 of 400 in 2x -> 200 due Nov 12 (settled!), 200 due Dec 12 (pending!)
            await purchases.AddAsync(CreditCardPurchase.Create(
                Guid.CreateVersion7(), accountId, archivedCardId, new DateOnly(2026, 10, 10), Money.FromDecimal(400.00m),
                2, 1, archivedCard.BillingRule, PurchaseCondition.Own, "Parcela antiga", SystemCategories.ShoppingId));

            await uow.SaveChangesAsync();
        }

        await using (var scope = _fixture.CreateScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var guard = new AccountAccessGuard(accounts, new FakeCurrentUser(userId));
            var handler = new GetMonthlySurplusHandler(
                guard,
                scope.ServiceProvider.GetRequiredService<IIncomeRepository>(),
                scope.ServiceProvider.GetRequiredService<IDirectExpenseRepository>(),
                scope.ServiceProvider.GetRequiredService<IFixedExpenseRepository>(),
                scope.ServiceProvider.GetRequiredService<ICreditCardPurchaseRepository>(),
                new FakeClock(today));

            var decDto = await handler.ExecuteAsync(
                new GetMonthlySurplusQuery(accountId, 2026, 12, PastMonths: 6, FutureMonths: 2));

            // Series covers Oct, Nov, Dec, Jan (4 points; clipped at Oct initial date)
            Assert.Equal(4, decDto.Series.Count);

            // Oct (historical): 2000 + 4000 - 500 - 1000 = 4500 (variation +2500)
            var oct = decDto.Series[0];
            Assert.True(oct.IsHistorical);
            Assert.Equal(4500.00m, oct.ProjectedSurplus);
            Assert.Equal(2500.00m, oct.NetMonthlyVariation);

            // Nov (current): cash on Nov 20 = 4500 - 300 (direct Nov 10) - 200 (archived card due Nov 12) = 4000
            // Remaining Nov: +4000 income - 1200 rent = 6800 (variation = 6800 - 4500 = +2300)
            var nov = decDto.Series[1];
            Assert.True(nov.IsCurrentMonth);
            Assert.Equal(4000.00m, nov.StartingCashBalance);
            Assert.Equal(4000.00m, nov.ExpectedIncomes);
            Assert.Equal(0.00m, nov.CardInvoiceDue);
            Assert.Equal(1200.00m, nov.FixedExpenses);
            Assert.Equal(6800.00m, nov.ProjectedSurplus);
            Assert.Equal(2300.00m, nov.NetMonthlyVariation);

            // Dec (selected future): starts 6800 - 500 (300 active + 200 archived) - 1200 rent = 5100 (variation -1700)
            var dec = decDto.SelectedMonth;
            Assert.Equal(dec, decDto.Series[2]);
            Assert.Equal(6800.00m, dec.StartingCashBalance);
            Assert.Equal(500.00m, dec.CardInvoiceDue);
            Assert.Equal(1200.00m, dec.FixedExpenses);
            Assert.Equal(5100.00m, dec.ProjectedSurplus);
            Assert.Equal(-1700.00m, dec.NetMonthlyVariation);

            // Jan: starts 5100 - 300 (active card 2nd inst) - 1200 rent = 3600 (variation -1500)
            var jan = decDto.Series[3];
            Assert.Equal(5100.00m, jan.StartingCashBalance);
            Assert.Equal(300.00m, jan.CardInvoiceDue);
            Assert.Equal(1200.00m, jan.FixedExpenses);
            Assert.Equal(3600.00m, jan.ProjectedSurplus);
            Assert.Equal(-1500.00m, jan.NetMonthlyVariation);

            // Average variation across 4 months = (2500 + 2300 - 1700 - 1500) / 4 = 1600 / 4 = +400.00
            Assert.Equal(400.00m, decDto.AverageMonthlyVariation);
        }
    }
}
