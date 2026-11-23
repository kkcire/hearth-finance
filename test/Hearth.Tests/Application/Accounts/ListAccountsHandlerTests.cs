using Hearth.Application.Accounts.ListAccounts;
using Hearth.Application.Authorization;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Accounts;

public class ListAccountsHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly ListAccountsHandler _handler;

    public ListAccountsHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new ListAccountsHandler(_accountAccess, _accounts);
    }

    [Fact]
    public async Task ExecuteAsync_NoAccounts_ReturnsEmptyList()
    {
        var result = await _handler.ExecuteAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleAccounts_ReturnsAllMappedDtos()
    {
        var first = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Nubank",
            Money.FromDecimal(1000.00m),
            new DateOnly(2026, 10, 1),
            isInitialBalanceConfirmed: true);
        var second = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Inter",
            Money.FromDecimal(-250.50m),
            new DateOnly(2026, 11, 1),
            isInitialBalanceConfirmed: false);
        await _accounts.AddAsync(first);
        await _accounts.AddAsync(second);

        var result = await _handler.ExecuteAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, dto => dto.Id == first.Id
                                       && dto.Name == "Nubank"
                                       && dto.InitialBalance == 1000.00m
                                       && dto.InitialBalanceDate == new DateOnly(2026, 10, 1)
                                       && dto.IsInitialBalanceConfirmed);
        Assert.Contains(result, dto => dto.Id == second.Id
                                       && dto.Name == "Inter"
                                       && dto.InitialBalance == -250.50m
                                       && dto.InitialBalanceDate == new DateOnly(2026, 11, 1)
                                       && !dto.IsInitialBalanceConfirmed);
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsCancellationToken()
    {
        using var cts = new CancellationTokenSource();

        await _handler.ExecuteAsync(cts.Token);

        Assert.Equal(cts.Token, _accounts.LastCancellationToken);
    }

    [Fact]
    public void Constructor_NullRepository_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ListAccountsHandler(null!, _accounts));
    }
}
