using Hearth.Application.Authorization;
using Hearth.Application.Categories.ListCategories;
using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Categories;

public class ListCategoriesHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly ListCategoriesHandler _handler;

    public ListCategoriesHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new ListCategoriesHandler(_accountAccess, _categories);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSystemAndOwnCategories_ExcludesOtherUsersAndArchivedByDefault()
    {
        var otherUserId = Guid.NewGuid();
        var pets = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Pets");
        var college = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Faculdade");
        college.Archive();
        var otherUserCategory = Category.CreateForUser(Guid.NewGuid(), otherUserId, "Outro Usuário");

        await _categories.AddAsync(pets);
        await _categories.AddAsync(college);
        await _categories.AddAsync(otherUserCategory);

        var activeOnly = await _handler.ExecuteAsync(new ListCategoriesQuery());
        Assert.Equal(SystemCategories.Definitions.Count + 1, activeOnly.Count);
        Assert.Contains(activeOnly, c => c.Id == SystemCategories.TransportId && c.IsSystem);
        Assert.Contains(activeOnly, c => c.Id == pets.Id && !c.IsSystem);
        Assert.DoesNotContain(activeOnly, c => c.Id == college.Id);
        Assert.DoesNotContain(activeOnly, c => c.Id == otherUserCategory.Id);

        var all = await _handler.ExecuteAsync(new ListCategoriesQuery(IncludeArchived: true));
        Assert.Equal(SystemCategories.Definitions.Count + 2, all.Count);
        Assert.Contains(all, c => c.Id == college.Id && c.IsArchived && !c.IsSystem);
        Assert.DoesNotContain(all, c => c.Id == otherUserCategory.Id);
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new ListCategoriesHandler(null!, _categories));
        Assert.Throws<ArgumentNullException>(() => new ListCategoriesHandler(_accountAccess, null!));
    }
}
