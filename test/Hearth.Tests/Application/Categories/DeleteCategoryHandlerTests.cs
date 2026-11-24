using Hearth.Application.Authorization;
using Hearth.Application.Categories.DeleteCategory;
using Hearth.Application.Exceptions;
using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Categories;

public class DeleteCategoryHandlerTests
{
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly Guid _cardId = Guid.NewGuid();
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly InMemoryDirectExpenseRepository _directExpenses = new();
    private readonly InMemoryFixedExpenseRepository _fixedExpenses = new();
    private readonly InMemoryCreditCardPurchaseRepository _purchases = new();
    private readonly InMemoryCategoryBudgetRepository _budgets = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly DeleteCategoryHandler _handler;

    public DeleteCategoryHandlerTests()
    {
        _accounts.AddAsync(Account.Create(
            _accountId,
            FakeCurrentUser.DefaultUserId,
            "Conta",
            Money.Zero,
            new DateOnly(2026, 1, 1))).GetAwaiter().GetResult();
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new DeleteCategoryHandler(
            _accountAccess,
            _categories,
            _directExpenses,
            _fixedExpenses,
            _purchases,
            _budgets,
            _unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_UnusedCategory_DeletesAndSaves()
    {
        var category = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Temporária");
        await _categories.AddAsync(category);

        await _handler.ExecuteAsync(new DeleteCategoryCommand(category.Id));

        Assert.Null(await _categories.GetByIdAsync(category.Id));
        Assert.Equal(1, _categories.DeleteCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_SystemCategory_ThrowsDomainException()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _handler.ExecuteAsync(new DeleteCategoryCommand(SystemCategories.TransportId)));

        Assert.NotNull(await _categories.GetByIdAsync(SystemCategories.TransportId));
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UsedByDirectExpense_ThrowsDomainException()
    {
        var category = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Lanches");
        await _categories.AddAsync(category);
        await _directExpenses.AddAsync(DirectExpense.Create(
            Guid.NewGuid(), _accountId, new DateOnly(2026, 10, 10), Money.FromDecimal(20m), "Lanche", category.Id));

        await Assert.ThrowsAsync<DomainException>(() =>
            _handler.ExecuteAsync(new DeleteCategoryCommand(category.Id)));

        Assert.NotNull(await _categories.GetByIdAsync(category.Id));
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UsedByFixedExpense_ThrowsDomainException()
    {
        var category = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Assinaturas");
        await _categories.AddAsync(category);
        await _fixedExpenses.AddAsync(FixedExpense.Create(
            Guid.NewGuid(), _accountId, YearMonth.Of(2026, 1), null, Money.FromDecimal(100m), "Internet", category.Id));

        await Assert.ThrowsAsync<DomainException>(() =>
            _handler.ExecuteAsync(new DeleteCategoryCommand(category.Id)));

        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UsedByCreditCardPurchase_ThrowsDomainException()
    {
        var category = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Eletrônicos");
        await _categories.AddAsync(category);
        await _purchases.AddAsync(CreditCardPurchase.Create(
            Guid.NewGuid(),
            _accountId,
            _cardId,
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(100m),
            1,
            1,
            CardBillingRule.Of(3, 12),
            SystemPurchaseConditions.OwnId,
            "Compra",
            category.Id));

        await Assert.ThrowsAsync<DomainException>(() =>
            _handler.ExecuteAsync(new DeleteCategoryCommand(category.Id)));

        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UsedByCategoryBudget_ThrowsDomainException()
    {
        var category = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Viagens");
        await _categories.AddAsync(category);
        await _budgets.AddAsync(CategoryBudget.Create(
            Guid.NewGuid(), _accountId, category.Id, YearMonth.Of(2026, 10), null, Money.FromDecimal(160m)));

        await Assert.ThrowsAsync<DomainException>(() =>
            _handler.ExecuteAsync(new DeleteCategoryCommand(category.Id)));

        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownCategory_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.ExecuteAsync(new DeleteCategoryCommand(unknownId)));

        Assert.Equal(nameof(Category), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new DeleteCategoryHandler(null!, _categories, _directExpenses, _fixedExpenses, _purchases, _budgets, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new DeleteCategoryHandler(_accountAccess, null!, _directExpenses, _fixedExpenses, _purchases, _budgets, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new DeleteCategoryHandler(_accountAccess, _categories, null!, _fixedExpenses, _purchases, _budgets, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new DeleteCategoryHandler(_accountAccess, _categories, _directExpenses, null!, _purchases, _budgets, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new DeleteCategoryHandler(_accountAccess, _categories, _directExpenses, _fixedExpenses, null!, _budgets, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new DeleteCategoryHandler(_accountAccess, _categories, _directExpenses, _fixedExpenses, _purchases, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new DeleteCategoryHandler(_accountAccess, _categories, _directExpenses, _fixedExpenses, _purchases, _budgets, null!));
    }
}
