using Hearth.Application.Authorization;
using Hearth.Application.Accounts.GetAccount;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Accounts;

public class GetAccountHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly GetAccountHandler _handler;

    public GetAccountHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new GetAccountHandler(_accountAccess);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingAccount_ReturnsDto()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta principal",
            Money.FromDecimal(1500.50m),
            new DateOnly(2026, 10, 1),
            CardBillingRule.Of(3, 12));
        await _accounts.AddAsync(account);

        var dto = await _handler.ExecuteAsync(new GetAccountQuery(account.Id));

        Assert.Equal(account.Id, dto.Id);
        Assert.Equal("Conta principal", dto.Name);
        Assert.Equal(1500.50m, dto.InitialBalance);
        Assert.Equal(new DateOnly(2026, 10, 1), dto.InitialBalanceDate);
        Assert.Equal(3, dto.ClosingDay);
        Assert.Equal(12, dto.DueDay);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.ExecuteAsync(new GetAccountQuery(unknownId)));

        Assert.Equal(nameof(Account), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
    }

    [Fact]
    public async Task ExecuteAsync_NullQuery_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.ExecuteAsync(null!));
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsCancellationToken()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta",
            Money.Zero,
            new DateOnly(2026, 10, 1),
            CardBillingRule.Of(3, 12));
        await _accounts.AddAsync(account);
        using var cts = new CancellationTokenSource();

        await _handler.ExecuteAsync(new GetAccountQuery(account.Id), cts.Token);

        Assert.Equal(cts.Token, _accounts.LastCancellationToken);
    }

    [Fact]
    public void Constructor_NullRepository_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new GetAccountHandler(null!));
    }
}
