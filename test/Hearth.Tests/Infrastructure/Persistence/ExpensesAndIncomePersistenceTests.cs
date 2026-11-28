using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hearth.Tests.Infrastructure.Persistence;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class ExpensesAndIncomePersistenceTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public ExpensesAndIncomePersistenceTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(Guid AccountId, Guid CardId, Guid CategoryId, Guid ConditionId)> SeedAccountCardAndCategoryAsync()
    {
        var userId = Guid.CreateVersion7();
        var accountId = Guid.CreateVersion7();
        var cardId = Guid.CreateVersion7();
        var categoryId = Guid.CreateVersion7();
        var conditionId = Guid.CreateVersion7();

        await using var scope = _fixture.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var cards = scope.ServiceProvider.GetRequiredService<ICreditCardRepository>();
        var categories = scope.ServiceProvider.GetRequiredService<ICategoryRepository>();
        var conditions = scope.ServiceProvider.GetRequiredService<IPurchaseConditionRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await users.AddAsync(User.Create(
            userId, "Erick", $"erick-{userId:N}@hearth.app", "hash-placeholder", new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero)));
        await accounts.AddAsync(Account.Create(
            accountId, userId, "Conta", Money.FromDecimal(1000.00m), TestTime.UtcOn(new DateOnly(2026, 1, 1))));
        await cards.AddAsync(CreditCard.Create(
            cardId, accountId, CardBillingRule.Of(3, 12), "Nubank", Money.FromDecimal(3000.00m)));
        await categories.AddAsync(Category.CreateForUser(categoryId, userId, "Geral"));
        await conditions.AddAsync(PurchaseCondition.CreateForUser(conditionId, userId, "Viagem"));
        await uow.SaveChangesAsync();

        return (accountId, cardId, categoryId, conditionId);
    }

    [Fact]
    public async Task FixedExpenseRepository_PersistsAmountHistoryPaymentsUpdatesAndCascadeDelete()
    {
        var (accountId, _, categoryId, _) = await SeedAccountCardAndCategoryAsync();
        var expenseId = Guid.CreateVersion7();

        await using (var scope = _fixture.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IFixedExpenseRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var expense = FixedExpense.Create(
                expenseId, accountId, YearMonth.Of(2026, 1), null, Money.FromDecimal(1000.00m), "Aluguel", categoryId);
            expense.MarkAsPaid(YearMonth.Of(2026, 1), new DateOnly(2026, 1, 5), new DateOnly(2026, 12, 31));

            await repo.AddAsync(expense);
            await uow.SaveChangesAsync();
        }

        // Mutate tracked entity: add amount change, pay another month, revert first month, set EndMonth.
        await using (var scope = _fixture.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IFixedExpenseRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var loaded = await repo.GetByIdAsync(expenseId);
            Assert.NotNull(loaded);
            Assert.True(loaded.IsPaid(YearMonth.Of(2026, 1)));

            loaded.ChangeAmount(Money.FromDecimal(1250.00m), new DateOnly(2026, 7, 10)); // effective 2026-07
            loaded.MarkAsPaid(YearMonth.Of(2026, 7), new DateOnly(2026, 7, 4), new DateOnly(2026, 12, 31));
            loaded.RevertPayment(YearMonth.Of(2026, 1));
            loaded.End(YearMonth.Of(2026, 12), new DateOnly(2026, 7, 10));

            await repo.UpdateAsync(loaded);
            await uow.SaveChangesAsync();
        }

        await using (var scope = _fixture.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IFixedExpenseRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            Assert.True(await repo.AnyByCategoryAsync(categoryId));

            var reloaded = await repo.GetByIdAsync(expenseId);
            Assert.NotNull(reloaded);
            Assert.Equal(YearMonth.Of(2026, 12), reloaded.EndMonth);
            Assert.Equal(2, reloaded.AmountHistory.Count);
            Assert.Equal(Money.FromDecimal(1000.00m), reloaded.AmountIn(YearMonth.Of(2026, 6)));
            Assert.Equal(Money.FromDecimal(1250.00m), reloaded.AmountIn(YearMonth.Of(2026, 7)));
            Assert.False(reloaded.IsPaid(YearMonth.Of(2026, 1)));
            Assert.Equal(new DateOnly(2026, 7, 4), reloaded.PaidOn(YearMonth.Of(2026, 7)));

            await repo.DeleteAsync(reloaded);
            await uow.SaveChangesAsync();
        }

        await using (var scope = _fixture.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IFixedExpenseRepository>();
            Assert.Null(await repo.GetByIdAsync(expenseId));
            Assert.False(await repo.AnyByCategoryAsync(categoryId));
        }
    }

    [Fact]
    public async Task DirectExpenseIncomeAndCreditCardPurchase_RoundTripAllFieldsAndConditions()
    {
        var (accountId, cardId, categoryId, conditionId) = await SeedAccountCardAndCategoryAsync();
        var directId = Guid.CreateVersion7();
        var incomeId = Guid.CreateVersion7();
        var purchaseId = Guid.CreateVersion7();

        await using (var scope = _fixture.CreateScope())
        {
            var directs = scope.ServiceProvider.GetRequiredService<IDirectExpenseRepository>();
            var incomes = scope.ServiceProvider.GetRequiredService<IIncomeRepository>();
            var purchases = scope.ServiceProvider.GetRequiredService<ICreditCardPurchaseRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await directs.AddAsync(DirectExpense.Create(
                directId, accountId, new DateOnly(2026, 10, 15), Money.FromDecimal(89.90m), "Mercado", categoryId));

            var income = Income.Create(
                incomeId, accountId, new DateOnly(2026, 11, 2), Money.FromDecimal(5000.00m), "Salário", conditionId);
            await incomes.AddAsync(income);

            await purchases.AddAsync(CreditCardPurchase.Create(
                purchaseId,
                accountId,
                cardId,
                new DateOnly(2026, 10, 10),
                Money.FromDecimal(300.00m),
                3,
                2,
                CardBillingRule.Of(3, 12),
                conditionId,
                "Hotel",
                categoryId));

            await uow.SaveChangesAsync();
        }

        await using (var scope = _fixture.CreateScope())
        {
            var directs = scope.ServiceProvider.GetRequiredService<IDirectExpenseRepository>();
            var incomes = scope.ServiceProvider.GetRequiredService<IIncomeRepository>();
            var purchases = scope.ServiceProvider.GetRequiredService<ICreditCardPurchaseRepository>();

            var direct = await directs.GetByIdAsync(directId);
            Assert.NotNull(direct);
            Assert.Equal(Money.FromDecimal(89.90m), direct.Amount);
            Assert.True(await directs.AnyByCategoryAsync(categoryId));

            var income = await incomes.GetByIdAsync(incomeId);
            Assert.NotNull(income);
            Assert.Equal(new DateOnly(2026, 11, 2), income.Date);
            Assert.Equal(YearMonth.Of(2026, 11), income.Month);
            Assert.True(income.IsReceivedOn(new DateOnly(2026, 11, 2)));
            Assert.False(income.IsReceivedOn(new DateOnly(2026, 11, 1)));
            Assert.Equal(conditionId, income.ConditionId);
            Assert.False(income.IsOwn);

            var purchase = await purchases.GetByIdAsync(purchaseId);
            Assert.NotNull(purchase);
            Assert.Equal(cardId, purchase.CreditCardId);
            Assert.Equal(conditionId, purchase.ConditionId);
            Assert.Equal(CardBillingRule.Of(3, 12), purchase.Rule);
            Assert.Equal(2, purchase.InitialInstallment);
            Assert.True(await purchases.AnyByCategoryAsync(categoryId));
            Assert.True(await purchases.AnyByCreditCardAsync(cardId));
        }
    }

    [Fact]
    public async Task FixedIncomeRepository_PersistsAmountHistoryReceiptsUpdatesAndCascadeDeleteViaSplitQuery()
    {
        var (accountId, _, _, conditionId) = await SeedAccountCardAndCategoryAsync();
        var incomeId = Guid.CreateVersion7();

        await using (var scope = _fixture.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IFixedIncomeRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var fixedIncome = FixedIncome.Create(
                incomeId, accountId, YearMonth.Of(2026, 1), null, 5, Money.FromDecimal(4000.00m), "Salário", conditionId);
            fixedIncome.ChangeAmountFrom(YearMonth.Of(2026, 7), Money.FromDecimal(4500.00m));
            fixedIncome.ConfirmReceipt(YearMonth.Of(2026, 1), new DateOnly(2026, 1, 5), Money.FromDecimal(4200.00m), new DateOnly(2026, 12, 31));

            await repo.AddAsync(fixedIncome);
            await uow.SaveChangesAsync();
        }

        // Reload via split query, mutate collections (add receipt, revert old receipt, update details & stop)
        await using (var scope = _fixture.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IFixedIncomeRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var loaded = await repo.GetByIdAsync(incomeId);
            Assert.NotNull(loaded);
            Assert.Equal(2, loaded.AmountHistory.Count);
            Assert.Single(loaded.Receipts);
            Assert.Equal(Money.FromDecimal(4200.00m), loaded.GetReceiptFor(YearMonth.Of(2026, 1))!.ActualAmount);

            loaded.UpdateDetails("Salário CLT", 6, conditionId);
            loaded.ConfirmReceipt(YearMonth.Of(2026, 7), new DateOnly(2026, 7, 6), Money.FromDecimal(4800.00m), new DateOnly(2026, 12, 31));
            loaded.RevertReceipt(YearMonth.Of(2026, 1));
            loaded.StopAt(YearMonth.Of(2026, 12));

            await repo.UpdateAsync(loaded);
            await uow.SaveChangesAsync();
        }

        await using (var scope = _fixture.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IFixedIncomeRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var byAccount = await repo.GetByAccountAsync(accountId);
            var reloaded = Assert.Single(byAccount);
            Assert.Equal("Salário CLT", reloaded.Description);
            Assert.Equal(6, reloaded.ExpectedDayOfMonth);
            Assert.Equal(YearMonth.Of(2026, 12), reloaded.EndMonth);
            Assert.Equal(2, reloaded.AmountHistory.Count);
            Assert.Equal(Money.FromDecimal(4000.00m), reloaded.GetExpectedAmountFor(YearMonth.Of(2026, 6)));
            Assert.Equal(Money.FromDecimal(4500.00m), reloaded.GetExpectedAmountFor(YearMonth.Of(2026, 7)));
            Assert.False(reloaded.IsReceivedIn(YearMonth.Of(2026, 1)));
            var julReceipt = reloaded.GetReceiptFor(YearMonth.Of(2026, 7));
            Assert.NotNull(julReceipt);
            Assert.Equal(new DateOnly(2026, 7, 6), julReceipt.ReceivedOn);
            Assert.Equal(Money.FromDecimal(4800.00m), julReceipt.ActualAmount);

            await repo.DeleteAsync(reloaded);
            await uow.SaveChangesAsync();
        }

        await using (var scope = _fixture.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IFixedIncomeRepository>();
            Assert.Null(await repo.GetByIdAsync(incomeId));
        }
    }
}
