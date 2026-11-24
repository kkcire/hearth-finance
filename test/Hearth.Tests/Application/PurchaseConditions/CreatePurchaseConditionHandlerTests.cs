using Hearth.Application.Authorization;
using Hearth.Application.PurchaseConditions.CreatePurchaseCondition;
using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.PurchaseConditions;

public class CreatePurchaseConditionHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryPurchaseConditionRepository _conditions = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly CreatePurchaseConditionHandler _handler;

    public CreatePurchaseConditionHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new CreatePurchaseConditionHandler(_accountAccess, _conditions, _unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_NewName_CreatesUserConditionTrimsAndSaves()
    {
        var dto = await _handler.ExecuteAsync(new CreatePurchaseConditionCommand("  Mãe  "));

        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal("Mãe", dto.Name);
        Assert.False(dto.IsSystem);
        Assert.False(dto.IsArchived);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);

        var saved = await _conditions.GetByIdAsync(dto.Id);
        Assert.NotNull(saved);
        Assert.Equal(FakeCurrentUser.DefaultUserId, saved.OwnerUserId);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingActiveUserConditionWithDifferentCaseAndSpaces_ReusesExistingWithoutSaving()
    {
        var first = await _handler.ExecuteAsync(new CreatePurchaseConditionCommand("Mãe"));
        var second = await _handler.ExecuteAsync(new CreatePurchaseConditionCommand("  mãe  "));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("Mãe", second.Name);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_NameMatchingGlobalSystemCondition_ReturnsGlobalWithoutDuplicating()
    {
        var own = await _handler.ExecuteAsync(new CreatePurchaseConditionCommand("  PRÓPRIA  "));
        var others = await _handler.ExecuteAsync(new CreatePurchaseConditionCommand("  outros  "));

        Assert.Equal(SystemPurchaseConditions.OwnId, own.Id);
        Assert.Equal("Própria", own.Name);
        Assert.True(own.IsSystem);

        Assert.Equal(SystemPurchaseConditions.OthersId, others.Id);
        Assert.Equal("Outros", others.Name);
        Assert.True(others.IsSystem);

        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingArchivedUserCondition_RestoresAndReturnsIt()
    {
        var archived = PurchaseCondition.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Viagem");
        archived.Archive();
        await _conditions.AddAsync(archived);

        var dto = await _handler.ExecuteAsync(new CreatePurchaseConditionCommand(" viagem "));

        Assert.Equal(archived.Id, dto.Id);
        Assert.Equal("Viagem", dto.Name);
        Assert.False(dto.IsArchived);
        Assert.False(archived.IsArchived);
        Assert.Equal(1, _conditions.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_SameNameOwnedByAnotherUser_CreatesSeparateConditionForCurrentUser()
    {
        var otherUserId = Guid.NewGuid();
        var otherCondition = PurchaseCondition.CreateForUser(Guid.NewGuid(), otherUserId, "Trabalho");
        await _conditions.AddAsync(otherCondition);

        var dto = await _handler.ExecuteAsync(new CreatePurchaseConditionCommand("Trabalho"));

        Assert.NotEqual(otherCondition.Id, dto.Id);
        Assert.Equal("Trabalho", dto.Name);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_InvalidName_ThrowsAndDoesNotSave(string? name)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            _handler.ExecuteAsync(new CreatePurchaseConditionCommand(name!)));

        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_NullCommand_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.ExecuteAsync(null!));
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new CreatePurchaseConditionHandler(null!, _conditions, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new CreatePurchaseConditionHandler(_accountAccess, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new CreatePurchaseConditionHandler(_accountAccess, _conditions, null!));
    }
}
