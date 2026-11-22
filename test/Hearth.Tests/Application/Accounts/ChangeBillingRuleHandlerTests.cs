using Hearth.Application.Authorization;
using Hearth.Application.Accounts.ChangeBillingRule;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Accounts;

public class ChangeBillingRuleHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly ChangeBillingRuleHandler _handler;

    public ChangeBillingRuleHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new ChangeBillingRuleHandler(_accountAccess, _accounts, _unitOfWork);
    }

    private async Task<Account> SeedAccountAsync()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta principal",
            Money.FromDecimal(1000.00m),
            new DateOnly(2026, 10, 1),
            CardBillingRule.Of(3, 12));

        await _accounts.AddAsync(account);
        return account;
    }

    [Fact]
    public async Task ExecuteAsync_ValidRule_UpdatesBillingRuleAndSaves()
    {
        var account = await SeedAccountAsync();

        await _handler.ExecuteAsync(new ChangeBillingRuleCommand(account.Id, 15, 25));

        var saved = await _accounts.GetByIdAsync(account.Id);
        Assert.NotNull(saved);
        Assert.Equal(CardBillingRule.Of(15, 25), saved.BillingRule);
        Assert.Equal(1, _accounts.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFoundAndDoesNotSave()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.ExecuteAsync(new ChangeBillingRuleCommand(unknownId, 15, 25)));

        Assert.Equal(nameof(Account), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
        Assert.Equal(0, _accounts.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(29, 12)]
    [InlineData(3, 0)]
    [InlineData(3, 29)]
    public async Task ExecuteAsync_InvalidDays_ThrowsKeepsRuleAndDoesNotSave(int closingDay, int dueDay)
    {
        var account = await SeedAccountAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _handler.ExecuteAsync(new ChangeBillingRuleCommand(account.Id, closingDay, dueDay)));

        Assert.Equal(CardBillingRule.Of(3, 12), account.BillingRule);
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

        await _handler.ExecuteAsync(new ChangeBillingRuleCommand(account.Id, 10, 20), cts.Token);

        Assert.Equal(cts.Token, _accounts.LastCancellationToken);
        Assert.Equal(cts.Token, _unitOfWork.LastCancellationToken);
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new ChangeBillingRuleHandler(null!, _accounts, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new ChangeBillingRuleHandler(_accountAccess, _accounts, null!));
    }
}
