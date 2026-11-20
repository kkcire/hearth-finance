using Hearth.Application.Categories.ListCategories;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Categories;

public class ListCategoriesHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly ListCategoriesHandler _handler;

    public ListCategoriesHandlerTests()
    {
        _handler = new ListCategoriesHandler(_accounts, _categories);
    }

    [Fact]
    public async Task ExecuteAsync_ExcludesArchivedByDefault_AndIncludesWhenRequested()
    {
        var account = Account.Create(
            Guid.NewGuid(), "Minha", Money.Zero, new DateOnly(2026, 1, 1), CardBillingRule.Of(3, 12));
        var otherAccount = Account.Create(
            Guid.NewGuid(), "Outra", Money.Zero, new DateOnly(2026, 1, 1), CardBillingRule.Of(3, 12));
        await _accounts.AddAsync(account);
        await _accounts.AddAsync(otherAccount);

        var food = Category.Create(Guid.NewGuid(), account.Id, "Alimentação");
        var transport = Category.Create(Guid.NewGuid(), account.Id, "Transporte");
        var college = Category.Create(Guid.NewGuid(), account.Id, "Faculdade");
        college.Archive();
        var other = Category.Create(Guid.NewGuid(), otherAccount.Id, "Outra");

        await _categories.AddAsync(transport);
        await _categories.AddAsync(food);
        await _categories.AddAsync(college);
        await _categories.AddAsync(other);

        var activeOnly = await _handler.ExecuteAsync(new ListCategoriesQuery(account.Id));
        Assert.Equal(2, activeOnly.Count);
        Assert.Equal("Alimentação", activeOnly[0].Name);
        Assert.Equal("Transporte", activeOnly[1].Name);

        var all = await _handler.ExecuteAsync(new ListCategoriesQuery(account.Id, IncludeArchived: true));
        Assert.Equal(3, all.Count);
        Assert.Contains(all, c => c.Id == college.Id && c.IsArchived);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.ExecuteAsync(new ListCategoriesQuery(unknownId)));

        Assert.Equal(nameof(Account), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new ListCategoriesHandler(null!, _categories));
        Assert.Throws<ArgumentNullException>(() => new ListCategoriesHandler(_accounts, null!));
    }
}
