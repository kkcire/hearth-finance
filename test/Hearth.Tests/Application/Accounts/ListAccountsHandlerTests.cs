using Hearth.Application.Accounts.ListAccounts;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Accounts;

public class ListAccountsHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly ListAccountsHandler _handler;

    public ListAccountsHandlerTests()
    {
        _handler = new ListAccountsHandler(_accounts);
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
            "Nubank",
            Money.FromDecimal(1000.00m),
            new DateOnly(2026, 10, 1),
            CardBillingRule.Of(3, 12));
        var second = Account.Create(
            Guid.NewGuid(),
            "Inter",
            Money.FromDecimal(-250.50m),
            new DateOnly(2026, 11, 1),
            CardBillingRule.Of(15, 22));
        await _accounts.AddAsync(first);
        await _accounts.AddAsync(second);

        var result = await _handler.ExecuteAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, dto => dto.Id == first.Id
                                       && dto.Name == "Nubank"
                                       && dto.InitialBalance == 1000.00m
                                       && dto.InitialBalanceDate == new DateOnly(2026, 10, 1)
                                       && dto.ClosingDay == 3
                                       && dto.DueDay == 12);
        Assert.Contains(result, dto => dto.Id == second.Id
                                       && dto.Name == "Inter"
                                       && dto.InitialBalance == -250.50m
                                       && dto.InitialBalanceDate == new DateOnly(2026, 11, 1)
                                       && dto.ClosingDay == 15
                                       && dto.DueDay == 22);
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
        Assert.Throws<ArgumentNullException>(() => new ListAccountsHandler(null!));
    }
}
