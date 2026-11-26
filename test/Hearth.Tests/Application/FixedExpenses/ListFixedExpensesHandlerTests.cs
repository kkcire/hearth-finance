using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Application.FixedExpenses.ListFixedExpenses;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.FixedExpenses;

public class ListFixedExpensesHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryFixedExpenseRepository _fixedExpenses = new();
    private readonly ListFixedExpensesHandler _handler;

    public ListFixedExpensesHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new ListFixedExpensesHandler(_accountAccess, _fixedExpenses);
    }

    private async Task<Account> SeedAccountAsync(string name = "Conta")
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            name,
            Money.Zero,
            TestTime.UtcOn(new DateOnly(2026, 1, 1)));
        await _accounts.AddAsync(account);
        return account;
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsOnlyAccountExpensesWithMappedHistoryAndPayments()
    {
        var account = await SeedAccountAsync("Minha");
        var otherAccount = await SeedAccountAsync("Outra");

        var rent = FixedExpense.Create(
            Guid.NewGuid(), account.Id, YearMonth.Of(2026, 1), null, Money.FromDecimal(1000.00m), "Aluguel", Guid.NewGuid());
        rent.ChangeAmount(Money.FromDecimal(1200.00m), new DateOnly(2026, 7, 10));
        rent.MarkAsPaid(YearMonth.Of(2026, 7), new DateOnly(2026, 7, 5), new DateOnly(2026, 12, 31));
        rent.MarkAsPaid(YearMonth.Of(2026, 6), new DateOnly(2026, 6, 5), new DateOnly(2026, 12, 31));

        var internet = FixedExpense.Create(
            Guid.NewGuid(), account.Id, YearMonth.Of(2026, 5), YearMonth.Of(2026, 12), Money.FromDecimal(99.90m), "Internet", Guid.NewGuid());

        var other = FixedExpense.Create(
            Guid.NewGuid(), otherAccount.Id, YearMonth.Of(2026, 10), null, Money.FromDecimal(500.00m), "Outro", Guid.NewGuid());

        await _fixedExpenses.AddAsync(rent);
        await _fixedExpenses.AddAsync(internet);
        await _fixedExpenses.AddAsync(other);

        var result = await _handler.ExecuteAsync(new ListFixedExpensesQuery(account.Id));

        Assert.Equal(2, result.Count);
        Assert.Equal(internet.Id, result[0].Id);
        Assert.Equal(2026, result[0].EndYear);
        Assert.Equal(12, result[0].EndMonth);

        Assert.Equal(rent.Id, result[1].Id);
        Assert.Null(result[1].EndYear);
        Assert.Equal(2, result[1].AmountHistory.Count);
        Assert.Equal(1000.00m, result[1].AmountHistory[0].Amount);
        Assert.Equal(1200.00m, result[1].AmountHistory[1].Amount);
        Assert.Equal(2, result[1].Payments.Count);
        Assert.Equal(6, result[1].Payments[0].Month);
        Assert.Equal(7, result[1].Payments[1].Month);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.ExecuteAsync(new ListFixedExpensesQuery(unknownId)));

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
        Assert.Throws<ArgumentNullException>(() => new ListFixedExpensesHandler(null!, _fixedExpenses));
        Assert.Throws<ArgumentNullException>(() => new ListFixedExpensesHandler(_accountAccess, null!));
    }
}
