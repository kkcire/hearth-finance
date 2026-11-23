using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Application.Incomes.ListIncomes;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Incomes;

public class ListIncomesHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryIncomeRepository _incomes = new();
    private readonly ListIncomesHandler _handler;

    public ListIncomesHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new ListIncomesHandler(_accountAccess, _incomes);
    }

    private async Task<Account> SeedAccountAsync(string name = "Conta")
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            name,
            Money.Zero,
            new DateOnly(2026, 10, 1));
        await _accounts.AddAsync(account);
        return account;
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsOnlyAccountIncomesOrderedByExpectedDateDescending()
    {
        var account = await SeedAccountAsync("Minha");
        var otherAccount = await SeedAccountAsync("Outra");

        var older = Income.Create(
            Guid.NewGuid(), account.Id, new DateOnly(2026, 10, 5), Money.FromDecimal(200.00m), "Pix");
        older.MarkAsReceived(new DateOnly(2026, 10, 6));

        var newer = Income.Create(
            Guid.NewGuid(), account.Id, new DateOnly(2026, 10, 28), Money.FromDecimal(5000.00m), "Salário");

        var other = Income.Create(
            Guid.NewGuid(), otherAccount.Id, new DateOnly(2026, 10, 30), Money.FromDecimal(9000.00m), "Outro");

        await _incomes.AddAsync(older);
        await _incomes.AddAsync(newer);
        await _incomes.AddAsync(other);

        var result = await _handler.ExecuteAsync(new ListIncomesQuery(account.Id));

        Assert.Equal(2, result.Count);
        Assert.Equal(newer.Id, result[0].Id);
        Assert.False(result[0].IsReceived);
        Assert.Null(result[0].ReceivedOn);

        Assert.Equal(older.Id, result[1].Id);
        Assert.True(result[1].IsReceived);
        Assert.Equal(new DateOnly(2026, 10, 6), result[1].ReceivedOn);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.ExecuteAsync(new ListIncomesQuery(unknownId)));

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
        Assert.Throws<ArgumentNullException>(() => new ListIncomesHandler(null!, _incomes));
        Assert.Throws<ArgumentNullException>(() => new ListIncomesHandler(_accountAccess, null!));
    }
}
