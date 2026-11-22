using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Authorization;

public sealed class AccountAccessGuardTests
{
    private readonly InMemoryAccountRepository _accounts = new();

    private static Account CreateAccount(Guid ownerUserId) =>
        Account.Create(
            Guid.NewGuid(),
            ownerUserId,
            "Conta",
            Money.FromDecimal(100m),
            new DateOnly(2026, 1, 1),
            CardBillingRule.Of(3, 12));

    [Fact]
    public async Task GetAuthorizedAccountAsync_OwnedAccount_ReturnsAccount()
    {
        var userId = Guid.NewGuid();
        var account = CreateAccount(userId);
        await _accounts.AddAsync(account);

        var guard = new AccountAccessGuard(_accounts, new FakeCurrentUser(userId));

        var result = await guard.GetAuthorizedAccountAsync(account.Id);

        Assert.Equal(account.Id, result.Id);
    }

    [Fact]
    public async Task GetAuthorizedAccountAsync_AccountOwnedByAnotherUser_ThrowsNotFound()
    {
        var ownerId = Guid.NewGuid();
        var intruderId = Guid.NewGuid();
        var account = CreateAccount(ownerId);
        await _accounts.AddAsync(account);

        var guard = new AccountAccessGuard(_accounts, new FakeCurrentUser(intruderId));

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => guard.GetAuthorizedAccountAsync(account.Id));

        Assert.Equal(nameof(Account), ex.ResourceName);
        Assert.Equal(account.Id, ex.ResourceId);
    }

    [Fact]
    public async Task GetAuthorizedAccountAsync_Unauthenticated_ThrowsUnauthorizedAccess()
    {
        var account = CreateAccount(Guid.NewGuid());
        await _accounts.AddAsync(account);

        var guard = new AccountAccessGuard(_accounts, FakeCurrentUser.Anonymous());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => guard.GetAuthorizedAccountAsync(account.Id));
    }

    [Fact]
    public async Task EnsureCanAccessEntityAsync_AccountOwnedByAnotherUser_ThrowsNotFoundWithEntityResource()
    {
        var ownerId = Guid.NewGuid();
        var intruderId = Guid.NewGuid();
        var account = CreateAccount(ownerId);
        await _accounts.AddAsync(account);

        var entityId = Guid.NewGuid();
        var guard = new AccountAccessGuard(_accounts, new FakeCurrentUser(intruderId));

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => guard.EnsureCanAccessEntityAsync(account.Id, nameof(DirectExpense), entityId));

        Assert.Equal(nameof(DirectExpense), ex.ResourceName);
        Assert.Equal(entityId, ex.ResourceId);
    }

    [Fact]
    public void GetRequiredUserId_Unauthenticated_ThrowsUnauthorizedAccess()
    {
        var guard = new AccountAccessGuard(_accounts, FakeCurrentUser.Anonymous());

        Assert.Throws<UnauthorizedAccessException>(() => guard.GetRequiredUserId());
    }
}
