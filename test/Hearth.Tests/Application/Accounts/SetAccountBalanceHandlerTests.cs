using Hearth.Application.Accounts.DeleteBalanceAdjustment;
using Hearth.Application.Accounts.SetAccountBalance;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Accounts;

public sealed class SetAccountBalanceHandlerTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 11, 15, 15, 0, 0, TimeSpan.Zero); // TrackingStartDate 2026-11-01

    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeClock _clock = new(new DateOnly(2026, 11, 20));
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly SetAccountBalanceHandler _handler;

    public SetAccountBalanceHandlerTests()
    {
        var guard = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new SetAccountBalanceHandler(guard, _accounts, _clock, _unitOfWork);
    }

    private async Task<Account> SeedAccountAsync(decimal openingBalance = 1000m)
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta Principal",
            Money.FromDecimal(openingBalance),
            CreatedAtUtc);
        await _accounts.AddAsync(account);
        return account;
    }

    [Fact]
    public async Task ExecuteAsync_WithPositiveZeroAndNegativeBalances_UpdatesSingleDay1OpeningBalanceAndRecordsAuditEntries()
    {
        var account = await SeedAccountAsync(1000m);

        var id1 = await _handler.ExecuteAsync(new SetAccountBalanceCommand(
            account.Id, 1842.50m, "Conferência extrato dia 1"));
        Assert.Equal(Money.FromDecimal(1842.50m), account.InitialBalance);

        var id2 = await _handler.ExecuteAsync(new SetAccountBalanceCommand(
            account.Id, 0m, "Conta zerada"));
        Assert.Equal(Money.Zero, account.InitialBalance);

        var id3 = await _handler.ExecuteAsync(new SetAccountBalanceCommand(
            account.Id, -150.25m, null));
        Assert.Equal(Money.FromDecimal(-150.25m), account.InitialBalance);

        Assert.NotEqual(Guid.Empty, id1);
        Assert.NotEqual(Guid.Empty, id2);
        Assert.NotEqual(Guid.Empty, id3);
        Assert.Equal(3, account.BalanceAdjustments.Count);
        Assert.All(account.BalanceAdjustments, entry => Assert.Equal(new DateOnly(2026, 11, 1), entry.Date));
        Assert.Equal(Money.FromDecimal(1842.50m), account.BalanceAdjustments[0].Amount);
        Assert.Equal("Conferência extrato dia 1", account.BalanceAdjustments[0].Reason);
        Assert.Equal(Money.Zero, account.BalanceAdjustments[1].Amount);
        Assert.Equal(Money.FromDecimal(-150.25m), account.BalanceAdjustments[2].Amount);
        Assert.Null(account.BalanceAdjustments[2].Reason);
        Assert.Equal(3, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithReasonExceedingMaxLength_ThrowsDomainException()
    {
        var account = await SeedAccountAsync();
        var tooLong = new string('a', 201);

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new SetAccountBalanceCommand(account.Id, 500m, tooLong)));

        Assert.Empty(account.BalanceAdjustments);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ForNonExistentAccount_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new SetAccountBalanceCommand(Guid.NewGuid(), 100m)));
    }

    [Fact]
    public async Task DeleteBalanceAdjustmentHandler_RemovesEntryAndRevertsOpeningBalance()
    {
        var account = await SeedAccountAsync(1000m);
        var guard = new AccountAccessGuard(_accounts, _currentUser);
        var deleteHandler = new DeleteBalanceAdjustmentHandler(guard, _accounts, _unitOfWork);

        var firstId = await _handler.ExecuteAsync(new SetAccountBalanceCommand(account.Id, 600m, "Correto"));
        _clock.UtcNow = _clock.UtcNow.AddMinutes(5);
        var wrongId = await _handler.ExecuteAsync(new SetAccountBalanceCommand(account.Id, 99999m, "Errado"));

        Assert.Equal(Money.FromDecimal(99999m), account.InitialBalance);

        await deleteHandler.ExecuteAsync(new DeleteBalanceAdjustmentCommand(account.Id, wrongId));
        Assert.Equal(Money.FromDecimal(600m), account.InitialBalance);
        Assert.Single(account.BalanceAdjustments);

        await deleteHandler.ExecuteAsync(new DeleteBalanceAdjustmentCommand(account.Id, firstId));
        Assert.Equal(Money.Zero, account.InitialBalance);
        Assert.Empty(account.BalanceAdjustments);

        await Assert.ThrowsAsync<NotFoundException>(() => deleteHandler.ExecuteAsync(
            new DeleteBalanceAdjustmentCommand(account.Id, Guid.NewGuid())));
    }
}
