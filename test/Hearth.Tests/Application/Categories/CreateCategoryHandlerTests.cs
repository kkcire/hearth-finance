using Hearth.Application.Categories.CreateCategory;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Categories;

public class CreateCategoryHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly CreateCategoryHandler _handler;

    public CreateCategoryHandlerTests()
    {
        _handler = new CreateCategoryHandler(_accounts, _categories, _unitOfWork);
    }

    private async Task<Account> SeedAccountAsync()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            "Conta",
            Money.Zero,
            new DateOnly(2026, 1, 1),
            CardBillingRule.Of(3, 12));
        await _accounts.AddAsync(account);
        return account;
    }

    [Fact]
    public async Task ExecuteAsync_ValidCommand_CreatesPersistsAndReturnsId()
    {
        var account = await SeedAccountAsync();

        var id = await _handler.ExecuteAsync(new CreateCategoryCommand(account.Id, "  Faculdade  "));

        var saved = await _categories.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(account.Id, saved.AccountId);
        Assert.Equal("Faculdade", saved.Name);
        Assert.False(saved.IsArchived);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateNameCaseInsensitiveInSameAccount_ThrowsDomainException()
    {
        var account = await SeedAccountAsync();
        await _categories.AddAsync(Category.Create(Guid.NewGuid(), account.Id, "Transporte"));

        await Assert.ThrowsAsync<DomainException>(() =>
            _handler.ExecuteAsync(new CreateCategoryCommand(account.Id, "  transporte  ")));

        Assert.Single(_categories.Categories);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_SameNameInDifferentAccount_IsAllowed()
    {
        var first = await SeedAccountAsync();
        var second = await SeedAccountAsync();
        await _categories.AddAsync(Category.Create(Guid.NewGuid(), first.Id, "Transporte"));

        var id = await _handler.ExecuteAsync(new CreateCategoryCommand(second.Id, "Transporte"));

        Assert.NotNull(await _categories.GetByIdAsync(id));
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.ExecuteAsync(new CreateCategoryCommand(unknownId, "Transporte")));

        Assert.Equal(nameof(Account), exception.ResourceName);
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
        Assert.Throws<ArgumentNullException>(() => new CreateCategoryHandler(null!, _categories, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new CreateCategoryHandler(_accounts, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new CreateCategoryHandler(_accounts, _categories, null!));
    }
}
