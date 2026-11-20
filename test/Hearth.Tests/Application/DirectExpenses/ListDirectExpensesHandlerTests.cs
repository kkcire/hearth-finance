using Hearth.Application.DirectExpenses.ListDirectExpenses;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.DirectExpenses;

public class ListDirectExpensesHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly InMemoryDirectExpenseRepository _directExpenses = new();
    private readonly ListDirectExpensesHandler _handler;

    public ListDirectExpensesHandlerTests()
    {
        _handler = new ListDirectExpensesHandler(_accounts, _directExpenses);
    }

    private async Task<Account> SeedAccountAsync(string name = "Conta")
    {
        var account = Account.Create(
            Guid.NewGuid(),
            name,
            Money.Zero,
            new DateOnly(2026, 10, 1),
            CardBillingRule.Of(3, 12));
        await _accounts.AddAsync(account);
        return account;
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsOnlyAccountExpensesOrderedByDateDescending()
    {
        var account = await SeedAccountAsync("Minha");
        var otherAccount = await SeedAccountAsync("Outra");

        var older = DirectExpense.Create(
            Guid.NewGuid(), account.Id, new DateOnly(2026, 10, 5), Money.FromDecimal(20.00m), "Café", ExpenseCategory.Food);
        var newer = DirectExpense.Create(
            Guid.NewGuid(), account.Id, new DateOnly(2026, 10, 20), Money.FromDecimal(100.00m), "Mercado", ExpenseCategory.Food);
        var other = DirectExpense.Create(
            Guid.NewGuid(), otherAccount.Id, new DateOnly(2026, 10, 25), Money.FromDecimal(500.00m), "Outro", ExpenseCategory.Shopping);

        await _directExpenses.AddAsync(older);
        await _directExpenses.AddAsync(newer);
        await _directExpenses.AddAsync(other);

        var result = await _handler.ExecuteAsync(new ListDirectExpensesQuery(account.Id));

        Assert.Equal(2, result.Count);
        Assert.Equal(newer.Id, result[0].Id);
        Assert.Equal(100.00m, result[0].Amount);
        Assert.Equal(older.Id, result[1].Id);
        Assert.Equal(20.00m, result[1].Amount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.ExecuteAsync(new ListDirectExpensesQuery(unknownId)));

        Assert.Equal(nameof(Account), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
    }

    [Fact]
    public async Task ExecuteAsync_NullQuery_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.ExecuteAsync(null!));
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new ListDirectExpensesHandler(null!, _directExpenses));
        Assert.Throws<ArgumentNullException>(() => new ListDirectExpensesHandler(_accounts, null!));
    }
}
