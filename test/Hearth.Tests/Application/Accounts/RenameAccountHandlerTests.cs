using Hearth.Application.Authorization;
using Hearth.Application.Accounts.RenameAccount;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Accounts;

public class RenameAccountHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RenameAccountHandler _handler;

    public RenameAccountHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new RenameAccountHandler(_accountAccess, _accounts, _unitOfWork);
    }

    private async Task<Account> SeedAccountAsync(string name = "Conta principal")
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            name,
            Money.FromDecimal(1000.00m),
            new DateOnly(2026, 10, 1),
            CardBillingRule.Of(3, 12));

        await _accounts.AddAsync(account);
        return account;
    }

    [Fact]
    public async Task ExecuteAsync_ExistingAccount_RenamesUpdatesAndSaves()
    {
        var account = await SeedAccountAsync();

        await _handler.ExecuteAsync(new RenameAccountCommand(account.Id, "  Nubank  "));

        var saved = await _accounts.GetByIdAsync(account.Id);
        Assert.NotNull(saved);
        Assert.Equal("Nubank", saved.Name);
        Assert.Equal(1, _accounts.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFoundAndDoesNotSave()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.ExecuteAsync(new RenameAccountCommand(unknownId, "Nubank")));

        Assert.Equal(nameof(Account), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
        Assert.Equal(0, _accounts.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_InvalidName_ThrowsKeepsNameAndDoesNotSave(string? name)
    {
        var account = await SeedAccountAsync("Conta principal");

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => _handler.ExecuteAsync(new RenameAccountCommand(account.Id, name!)));

        Assert.Equal("Conta principal", account.Name);
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

        await _handler.ExecuteAsync(new RenameAccountCommand(account.Id, "Nubank"), cts.Token);

        Assert.Equal(cts.Token, _accounts.LastCancellationToken);
        Assert.Equal(cts.Token, _unitOfWork.LastCancellationToken);
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new RenameAccountHandler(null!, _accounts, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new RenameAccountHandler(_accountAccess, _accounts, null!));
    }
}
