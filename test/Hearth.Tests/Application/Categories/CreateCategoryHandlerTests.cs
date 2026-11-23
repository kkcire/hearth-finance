using Hearth.Application.Authorization;
using Hearth.Application.Categories.CreateCategory;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Categories;

public class CreateCategoryHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly CreateCategoryHandler _handler;

    public CreateCategoryHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new CreateCategoryHandler(_accountAccess, _categories, _unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_ValidCommand_CreatesUserCategoryPersistsAndReturnsId()
    {
        var id = await _handler.ExecuteAsync(new CreateCategoryCommand("  Faculdade  "));

        var saved = await _categories.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(FakeCurrentUser.DefaultUserId, saved.OwnerUserId);
        Assert.False(saved.IsSystem);
        Assert.Equal("Faculdade", saved.Name);
        Assert.False(saved.IsArchived);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateNameCaseInsensitiveForSameUser_ThrowsDomainException()
    {
        await _categories.AddAsync(Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Faculdade"));

        await Assert.ThrowsAsync<DomainException>(() =>
            _handler.ExecuteAsync(new CreateCategoryCommand("  faculdade  ")));

        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Theory]
    [InlineData("Transporte")]
    [InlineData("transporte")]
    [InlineData("  ALIMENTAÇÃO  ")]
    [InlineData("saúde")]
    public async Task ExecuteAsync_CollidesWithSystemCategoryCaseInsensitive_ThrowsDomainException(string collidingName)
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _handler.ExecuteAsync(new CreateCategoryCommand(collidingName)));

        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_SameNameForDifferentUser_IsAllowed()
    {
        var otherUserId = Guid.NewGuid();
        await _categories.AddAsync(Category.CreateForUser(Guid.NewGuid(), otherUserId, "Faculdade"));

        var id = await _handler.ExecuteAsync(new CreateCategoryCommand("Faculdade"));

        var saved = await _categories.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(FakeCurrentUser.DefaultUserId, saved.OwnerUserId);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_NullCommand_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.ExecuteAsync(null!));
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new CreateCategoryHandler(null!, _categories, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new CreateCategoryHandler(_accountAccess, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new CreateCategoryHandler(_accountAccess, _categories, null!));
    }
}
