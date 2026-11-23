using Hearth.Application.Authorization;
using Hearth.Application.Categories.ArchiveCategory;
using Hearth.Application.Exceptions;
using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Categories;

public class ArchiveCategoryHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly ArchiveCategoryHandler _handler;

    public ArchiveCategoryHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new ArchiveCategoryHandler(_accountAccess, _categories, _unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_ActiveCategory_ArchivesAndSaves()
    {
        var category = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Faculdade");
        await _categories.AddAsync(category);

        var changed = await _handler.ExecuteAsync(new ArchiveCategoryCommand(category.Id));

        Assert.True(changed);
        Assert.True(category.IsArchived);
        Assert.Equal(1, _categories.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyArchived_IsIdempotentAndDoesNotSave()
    {
        var category = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Faculdade");
        category.Archive();
        await _categories.AddAsync(category);

        var changed = await _handler.ExecuteAsync(new ArchiveCategoryCommand(category.Id));

        Assert.False(changed);
        Assert.Equal(0, _categories.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_SystemCategory_ThrowsDomainException()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _handler.ExecuteAsync(new ArchiveCategoryCommand(SystemCategories.FoodId)));

        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownCategory_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.ExecuteAsync(new ArchiveCategoryCommand(unknownId)));

        Assert.Equal(nameof(Category), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new ArchiveCategoryHandler(null!, _categories, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new ArchiveCategoryHandler(_accountAccess, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new ArchiveCategoryHandler(_accountAccess, _categories, null!));
    }
}
