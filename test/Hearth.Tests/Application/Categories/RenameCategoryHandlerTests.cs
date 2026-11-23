using Hearth.Application.Authorization;
using Hearth.Application.Categories.RenameCategory;
using Hearth.Application.Exceptions;
using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Categories;

public class RenameCategoryHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RenameCategoryHandler _handler;

    public RenameCategoryHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new RenameCategoryHandler(_accountAccess, _categories, _unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_ValidNewName_RenamesAndSaves()
    {
        var category = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Faculdade");
        await _categories.AddAsync(category);

        await _handler.ExecuteAsync(new RenameCategoryCommand(category.Id, "  Pós-graduação  "));

        Assert.Equal("Pós-graduação", category.Name);
        Assert.Equal(1, _categories.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ChangingOnlyCasingOfOwnName_IsAllowed()
    {
        var category = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Faculdade");
        await _categories.AddAsync(category);

        await _handler.ExecuteAsync(new RenameCategoryCommand(category.Id, "FACULDADE"));

        Assert.Equal("FACULDADE", category.Name);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_CollidesWithAnotherUserCategoryCaseInsensitive_ThrowsAndRestoresOriginalName()
    {
        var first = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Faculdade");
        var second = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Pets");
        await _categories.AddAsync(first);
        await _categories.AddAsync(second);

        await Assert.ThrowsAsync<DomainException>(() =>
            _handler.ExecuteAsync(new RenameCategoryCommand(second.Id, "faculdade")));

        Assert.Equal("Pets", second.Name);
        Assert.Equal(0, _categories.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_CollidesWithSystemCategoryCaseInsensitive_ThrowsAndRestoresOriginalName()
    {
        var custom = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Pets");
        await _categories.AddAsync(custom);

        await Assert.ThrowsAsync<DomainException>(() =>
            _handler.ExecuteAsync(new RenameCategoryCommand(custom.Id, "transporte")));

        Assert.Equal("Pets", custom.Name);
        Assert.Equal(0, _categories.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_SystemCategory_ThrowsDomainException()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _handler.ExecuteAsync(new RenameCategoryCommand(SystemCategories.TransportId, "Mobilidade")));

        Assert.Equal(0, _categories.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownCategory_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.ExecuteAsync(new RenameCategoryCommand(unknownId, "Nova")));

        Assert.Equal(nameof(Category), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
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
        Assert.Throws<ArgumentNullException>(() => new RenameCategoryHandler(null!, _categories, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new RenameCategoryHandler(_accountAccess, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new RenameCategoryHandler(_accountAccess, _categories, null!));
    }
}
