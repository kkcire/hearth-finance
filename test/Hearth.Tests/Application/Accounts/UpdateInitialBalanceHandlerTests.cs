using Hearth.Application.Accounts.UpdateInitialBalance;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Accounts;

public class UpdateInitialBalanceHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly UpdateInitialBalanceHandler _handler;

    public UpdateInitialBalanceHandlerTests()
    {
        _handler = new UpdateInitialBalanceHandler(_accounts, _unitOfWork);
    }

    private async Task<Account> SeedAccountAsync(decimal initialBalance = 1000.00m)
    {
        var account = Account.Create(
            Guid.NewGuid(),
            "Conta principal",
            Money.FromDecimal(initialBalance),
            new DateOnly(2026, 10, 1),
            CardBillingRule.Of(3, 12));

        await _accounts.AddAsync(account);
        return account;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2500.75)]
    [InlineData(-320.40)]
    public async Task ExecuteAsync_ValidAmount_UpdatesBalanceKeepsDateAndSaves(double raw)
    {
        var account = await SeedAccountAsync();

        await _handler.ExecuteAsync(new UpdateInitialBalanceCommand(account.Id, (decimal)raw));

        var saved = await _accounts.GetByIdAsync(account.Id);
        Assert.NotNull(saved);
        Assert.Equal(Money.FromDecimal((decimal)raw), saved.InitialBalance);
        Assert.Equal(new DateOnly(2026, 10, 1), saved.InitialBalanceDate);
        Assert.Equal(1, _accounts.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFoundAndDoesNotSave()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.ExecuteAsync(new UpdateInitialBalanceCommand(unknownId, 500.00m)));

        Assert.Equal(nameof(Account), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
        Assert.Equal(0, _accounts.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_MoreThanTwoDecimalPlaces_ThrowsKeepsBalanceAndDoesNotSave()
    {
        var account = await SeedAccountAsync(1000.00m);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.ExecuteAsync(new UpdateInitialBalanceCommand(account.Id, 10.005m)));

        Assert.Equal(Money.FromDecimal(1000.00m), account.InitialBalance);
        Assert.Equal(0, _accounts.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_NullCommand_ThrowsAndDoesNotSave()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.ExecuteAsync(null!));

        Assert.Equal(0, _accounts.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsCancellationToken()
    {
        var account = await SeedAccountAsync();
        using var cts = new CancellationTokenSource();

        await _handler.ExecuteAsync(new UpdateInitialBalanceCommand(account.Id, 200.00m), cts.Token);

        Assert.Equal(cts.Token, _accounts.LastCancellationToken);
        Assert.Equal(cts.Token, _unitOfWork.LastCancellationToken);
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new UpdateInitialBalanceHandler(null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new UpdateInitialBalanceHandler(_accounts, null!));
    }
}
