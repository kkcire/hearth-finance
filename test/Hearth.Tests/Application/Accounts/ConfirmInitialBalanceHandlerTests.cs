using Hearth.Application.Accounts.ConfirmInitialBalance;
using Hearth.Application.Authorization;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Accounts;

public sealed class ConfirmInitialBalanceHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeClock _clock = new(new DateOnly(2026, 11, 22));
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly ConfirmInitialBalanceHandler _handler;

    public ConfirmInitialBalanceHandlerTests()
    {
        var guard = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new ConfirmInitialBalanceHandler(guard, _accounts, _clock, _unitOfWork);
    }

    private async Task<Account> SeedUnconfirmedAccountAsync()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta Principal",
            Money.Zero,
            _clock.Today,
            isInitialBalanceConfirmed: false);
        await _accounts.AddAsync(account);
        return account;
    }

    [Fact]
    public async Task ExecuteAsync_WithValidBalanceAndPastDate_ConfirmsAndSaves()
    {
        var account = await SeedUnconfirmedAccountAsync();

        await _handler.ExecuteAsync(new ConfirmInitialBalanceCommand(
            account.Id, 2450.75m, new DateOnly(2026, 11, 1)));

        Assert.Equal(Money.FromDecimal(2450.75m), account.InitialBalance);
        Assert.Equal(new DateOnly(2026, 11, 1), account.InitialBalanceDate);
        Assert.True(account.IsInitialBalanceConfirmed);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithSkipTrue_SetsZeroFromTodayAndMarksConfirmed()
    {
        var account = await SeedUnconfirmedAccountAsync();

        await _handler.ExecuteAsync(new ConfirmInitialBalanceCommand(
            account.Id, 999m, new DateOnly(2026, 10, 1), Skip: true));

        Assert.Equal(Money.Zero, account.InitialBalance);
        Assert.Equal(_clock.Today, account.InitialBalanceDate);
        Assert.True(account.IsInitialBalanceConfirmed);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithFutureDate_ThrowsDomainExceptionAndDoesNotSave()
    {
        var account = await SeedUnconfirmedAccountAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new ConfirmInitialBalanceCommand(account.Id, 100m, new DateOnly(2026, 11, 23))));

        Assert.False(account.IsInitialBalanceConfirmed);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAlreadyConfirmed_ThrowsDomainException()
    {
        var account = await SeedUnconfirmedAccountAsync();
        await _handler.ExecuteAsync(new ConfirmInitialBalanceCommand(account.Id, 0m, _clock.Today, Skip: true));

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new ConfirmInitialBalanceCommand(account.Id, 500m, _clock.Today)));
    }
}
