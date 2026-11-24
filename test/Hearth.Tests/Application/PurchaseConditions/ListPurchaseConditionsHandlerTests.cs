using Hearth.Application.Authorization;
using Hearth.Application.PurchaseConditions.ListPurchaseConditions;
using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.PurchaseConditions;

public class ListPurchaseConditionsHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryPurchaseConditionRepository _conditions = new();
    private readonly ListPurchaseConditionsHandler _handler;

    public ListPurchaseConditionsHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new ListPurchaseConditionsHandler(_accountAccess, _conditions);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSystemConditionFirstThenUserActiveConditions_IsolatingOtherUsers()
    {
        var zebra = PurchaseCondition.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Zebra");
        var ana = PurchaseCondition.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Ana");
        var archived = PurchaseCondition.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Antiga");
        archived.Archive();
        var otherUser = PurchaseCondition.CreateForUser(Guid.NewGuid(), Guid.NewGuid(), "Segredo");

        await _conditions.AddAsync(zebra);
        await _conditions.AddAsync(ana);
        await _conditions.AddAsync(archived);
        await _conditions.AddAsync(otherUser);

        var activeOnly = await _handler.ExecuteAsync(new ListPurchaseConditionsQuery());
        Assert.Equal(4, activeOnly.Count);
        Assert.Equal(SystemPurchaseConditions.OwnId, activeOnly[0].Id);
        Assert.True(activeOnly[0].IsSystem);
        Assert.Equal(SystemPurchaseConditions.OthersId, activeOnly[1].Id);
        Assert.True(activeOnly[1].IsSystem);
        Assert.Equal("Ana", activeOnly[2].Name);
        Assert.Equal("Zebra", activeOnly[3].Name);

        var withArchived = await _handler.ExecuteAsync(new ListPurchaseConditionsQuery(IncludeArchived: true));
        Assert.Equal(5, withArchived.Count);
        Assert.Contains(withArchived, item => item.Id == archived.Id && item.IsArchived);
        Assert.DoesNotContain(withArchived, item => item.Id == otherUser.Id);
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new ListPurchaseConditionsHandler(null!, _conditions));
        Assert.Throws<ArgumentNullException>(() => new ListPurchaseConditionsHandler(_accountAccess, null!));
    }
}
