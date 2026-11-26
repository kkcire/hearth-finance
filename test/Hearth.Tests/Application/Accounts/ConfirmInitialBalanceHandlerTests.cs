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
    private static readonly DateOnly CreatedDate = new(2026, 11, 22);

    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly ConfirmInitialBalanceHandler _handler;

    public ConfirmInitialBalanceHandlerTests()
    {
        var guard = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new ConfirmInitialBalanceHandler(guard, _accounts, _unitOfWork);
    }

    private async Task<Account> SeedUnconfirmedAccountAsync()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta Principal",
            Money.Zero,
            TestTime.UtcOn(CreatedDate),
            isInitialBalanceConfirmed: false);
        await _accounts.AddAsync(account);
        return account;
    }

    [Fact]
    public async Task ExecuteAsync_WithValidBalance_ConfirmsKeepsCreationDateAndSaves()
    {
        var account = await SeedUnconfirmedAccountAsync();

        await _handler.ExecuteAsync(new ConfirmInitialBalanceCommand(account.Id, 2450.75m));

        Assert.Equal(Money.FromDecimal(2450.75m), account.InitialBalance);
        Assert.Equal(new DateOnly(2026, 11, 1), account.TrackingStartDate);
        Assert.True(account.IsInitialBalanceConfirmed);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithSkipTrue_SetsZeroKeepsCreationDateAndMarksConfirmed()
    {
        var account = await SeedUnconfirmedAccountAsync();

        await _handler.ExecuteAsync(new ConfirmInitialBalanceCommand(account.Id, 999m, Skip: true));

        Assert.Equal(Money.Zero, account.InitialBalance);
        Assert.Equal(new DateOnly(2026, 11, 1), account.TrackingStartDate);
        Assert.True(account.IsInitialBalanceConfirmed);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAlreadyConfirmed_ThrowsDomainException()
    {
        var account = await SeedUnconfirmedAccountAsync();
        await _handler.ExecuteAsync(new ConfirmInitialBalanceCommand(account.Id, 0m, Skip: true));

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new ConfirmInitialBalanceCommand(account.Id, 500m)));
    }
}
