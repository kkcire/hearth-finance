using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Invoices.GetInvoiceSummary;
using Hearth.Application.Reports.GetMonthlySurplus;
using Hearth.Application.Reports.GetProjectedResult;
using Hearth.Application.Reports.GetRealBalance;
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
                2, 1, activeCard.BillingRule, SystemPurchaseConditions.OwnId, "Eletro", SystemCategories.ShoppingId));

            // Archived card (3/12): purchase Oct 10 of 400 in 2x -> 200 due Nov 12 (settled!), 200 due Dec 12 (pending!)
            await purchases.AddAsync(CreditCardPurchase.Create(
                Guid.CreateVersion7(), accountId, archivedCardId, new DateOnly(2026, 10, 10), Money.FromDecimal(400.00m),
                2, 1, archivedCard.BillingRule, SystemPurchaseConditions.OwnId, "Parcela antiga", SystemCategories.ShoppingId));

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

    [Fact]
    public async Task ConditionFilter_EndToEndWithPostgres_FiltersSurplusInvoiceAndProjectedResultWithoutMaskingRealBalance()
    {
        var userId = Guid.CreateVersion7();
        var accountId = Guid.CreateVersion7();
        var cardId = Guid.CreateVersion7();
        var momConditionId = Guid.CreateVersion7();
        var today = new DateOnly(2026, 11, 15);

        await using (var scope = _fixture.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var cards = scope.ServiceProvider.GetRequiredService<ICreditCardRepository>();
            var conditions = scope.ServiceProvider.GetRequiredService<IPurchaseConditionRepository>();
            var incomes = scope.ServiceProvider.GetRequiredService<IIncomeRepository>();
            var directs = scope.ServiceProvider.GetRequiredService<IDirectExpenseRepository>();
            var fixedExpenses = scope.ServiceProvider.GetRequiredService<IFixedExpenseRepository>();
            var purchases = scope.ServiceProvider.GetRequiredService<ICreditCardPurchaseRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await users.AddAsync(User.Create(
                userId, "Erick", "surplus-cond@hearth.app", "hash", new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero)));
            await accounts.AddAsync(Account.Create(
                accountId, userId, "Conta principal", Money.FromDecimal(3000.00m), new DateOnly(2026, 10, 1), isInitialBalanceConfirmed: true));

            var card = CreditCard.Create(
                cardId, accountId, CardBillingRule.Of(25, 5), "Nubank", Money.FromDecimal(8000.00m));
            await cards.AddAsync(card);
            await conditions.AddAsync(PurchaseCondition.CreateForUser(momConditionId, userId, "Mãe"));

            // Overdue Oct pending income & fixed expense -> must NOT be dragged into Nov cash cascade
            await incomes.AddAsync(Income.Create(
                Guid.CreateVersion7(), accountId, new DateOnly(2026, 10, 20), Money.FromDecimal(500.00m), "Freela atrasado"));
            await fixedExpenses.AddAsync(FixedExpense.Create(
                Guid.CreateVersion7(), accountId, YearMonth.Of(2026, 10), YearMonth.Of(2026, 10), Money.FromDecimal(250.00m), "Conta Out", SystemCategories.ServicesId));

            // Nov direct expense already paid <= today (-200) and scheduled > today (-100)
            await directs.AddAsync(DirectExpense.Create(
                Guid.CreateVersion7(), accountId, new DateOnly(2026, 11, 10), Money.FromDecimal(200.00m), "Mercado", SystemCategories.FoodId));
            await directs.AddAsync(DirectExpense.Create(
                Guid.CreateVersion7(), accountId, new DateOnly(2026, 11, 22), Money.FromDecimal(100.00m), "Dentista", SystemCategories.HealthId));

            // Settled invoice on Nov 5 (<= today) of R$ 400 with "Outros": already left RealBalance!
            await purchases.AddAsync(CreditCardPurchase.Create(
                Guid.CreateVersion7(), accountId, cardId, new DateOnly(2026, 10, 10), Money.FromDecimal(400.00m),
                1, 1, card.BillingRule, SystemPurchaseConditions.OthersId, "Fatura paga Outros", SystemCategories.ShoppingId));

            // Future due Dec 5 (closes Nov 25): Own 300 (in 2x -> 150 Dec, 150 Jan) + Mom 500 (1x -> 500 Dec)
            await purchases.AddAsync(CreditCardPurchase.Create(
                Guid.CreateVersion7(), accountId, cardId, new DateOnly(2026, 11, 10), Money.FromDecimal(300.00m),
                2, 1, card.BillingRule, SystemPurchaseConditions.OwnId, "Tênis Próprio", SystemCategories.ShoppingId));
            await purchases.AddAsync(CreditCardPurchase.Create(
                Guid.CreateVersion7(), accountId, cardId, new DateOnly(2026, 11, 12), Money.FromDecimal(500.00m),
                1, 1, card.BillingRule, momConditionId, "Presente Mãe", SystemCategories.ShoppingId));

            await uow.SaveChangesAsync();
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

            // RealBalance on Nov 15 = 3000 - 200 (direct Nov 10) - 400 (Others invoice due Nov 5) = 2400
            var realBalance = await new GetRealBalanceHandler(guard, incomes, directs, fixedExpenses, purchases, clock)
                .ExecuteAsync(new GetRealBalanceQuery(accountId));
            Assert.Equal(2400.00m, realBalance.Balance);

            var surplusHandler = new GetMonthlySurplusHandler(guard, incomes, directs, fixedExpenses, purchases, clock);
            var allSurplus = await surplusHandler.ExecuteAsync(
                new GetMonthlySurplusQuery(accountId, 2026, 12, PastMonths: 1, FutureMonths: 2));
            var ownSurplus = await surplusHandler.ExecuteAsync(
                new GetMonthlySurplusQuery(accountId, 2026, 12, PastMonths: 1, FutureMonths: 2, ConditionId: SystemPurchaseConditions.OwnId));

            // StartingCashBalance in Nov is ALWAYS the exact unmasked RealBalance (2400)
            Assert.Equal(2400.00m, allSurplus.Series[1].StartingCashBalance);
            Assert.Equal(2400.00m, ownSurplus.Series[1].StartingCashBalance);
            // Nov pending direct = 100 (> today), overdue Oct items NOT dragged -> Nov closing surplus = 2300
            Assert.Equal(2300.00m, allSurplus.Series[1].ProjectedSurplus);
            Assert.Equal(2300.00m, ownSurplus.Series[1].ProjectedSurplus);

            // Dec (DueMonth for Nov 25 closing):
            // - All: 2300 - 650 (150 Own + 500 Mom) = 1650; Jan rollover: 1650 - 150 = 1500
            // - Own: 2300 - 150 (only Own) = 2150; Jan rollover: 2150 - 150 = 2000
            Assert.Equal(650.00m, allSurplus.SelectedMonth.CardInvoiceDue);
            Assert.Equal(1650.00m, allSurplus.SelectedMonth.ProjectedSurplus);
            Assert.Equal(1500.00m, allSurplus.Series[3].ProjectedSurplus);

            Assert.Equal(SystemPurchaseConditions.OwnId, ownSurplus.ConditionId);
            Assert.Equal(150.00m, ownSurplus.SelectedMonth.CardInvoiceDue);
            Assert.Equal(2150.00m, ownSurplus.SelectedMonth.ProjectedSurplus);
            Assert.Equal(2000.00m, ownSurplus.Series[3].ProjectedSurplus);

            // Invoice summary for Nov closing (due Dec 5) filters amount + installments by ConditionId
            var invoiceHandler = new GetInvoiceSummaryHandler(guard, cards, purchases, clock);
            var allNovInvoice = await invoiceHandler.ExecuteAsync(new GetInvoiceSummaryQuery(accountId, 2026, 11));
            var ownNovInvoice = await invoiceHandler.ExecuteAsync(
                new GetInvoiceSummaryQuery(accountId, 2026, 11, SystemPurchaseConditions.OwnId));
            Assert.Equal(650.00m, allNovInvoice.CalculatedAmount);
            Assert.Equal(2, allNovInvoice.Installments.Count);
            Assert.Equal(150.00m, ownNovInvoice.CalculatedAmount);
            Assert.Single(ownNovInvoice.Installments);

            // ProjectedResult (pure accrual for Nov): includes Nov-closing invoice (150 Own vs 650 All) + all Nov directs (300)
            var projectedHandler = new GetProjectedResultHandler(guard, incomes, directs, fixedExpenses, purchases);
            var allNovProjected = await projectedHandler.ExecuteAsync(new GetProjectedResultQuery(accountId, 2026, 11));
            var ownNovProjected = await projectedHandler.ExecuteAsync(
                new GetProjectedResultQuery(accountId, 2026, 11, SystemPurchaseConditions.OwnId));
            Assert.Equal(650.00m, allNovProjected.CardInvoice);
            Assert.Equal(-950.00m, allNovProjected.Result);
            Assert.Equal(150.00m, ownNovProjected.CardInvoice);
            Assert.Equal(-450.00m, ownNovProjected.Result);
        }
    }
}
