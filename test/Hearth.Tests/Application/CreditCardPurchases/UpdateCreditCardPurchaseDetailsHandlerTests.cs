using Hearth.Application.Authorization;
using Hearth.Application.CreditCardPurchases.UpdateCreditCardPurchaseDetails;
using Hearth.Application.Exceptions;
using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.CreditCardPurchases;

public class UpdateCreditCardPurchaseDetailsHandlerTests
{
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly Guid _cardId = Guid.NewGuid();
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly InMemoryPurchaseConditionRepository _conditions = new();
    private readonly InMemoryCreditCardPurchaseRepository _purchases = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly UpdateCreditCardPurchaseDetailsHandler _handler;

    public UpdateCreditCardPurchaseDetailsHandlerTests()
    {
        _accounts.AddAsync(Account.Create(
            _accountId,
            FakeCurrentUser.DefaultUserId,
            "Conta",
            Money.Zero,
            TestTime.UtcOn(new DateOnly(2026, 1, 1)))).GetAwaiter().GetResult();
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new UpdateCreditCardPurchaseDetailsHandler(_accountAccess, _categories, _conditions, _purchases, _unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingPurchase_UpdatesDescriptionCategoryAndConditionAndSaves()
    {
        var shopping = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Eletrônicos");
        var personal = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Presentes");
        var momCondition = PurchaseCondition.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Mãe");
        await _categories.AddAsync(shopping);
        await _categories.AddAsync(personal);
        await _conditions.AddAsync(momCondition);

        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(),
            _accountId,
            _cardId,
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(300.00m),
            3,
            1,
            CardBillingRule.Of(3, 12),
            SystemPurchaseConditions.OwnId,
            "Compra",
            shopping.Id);
        await _purchases.AddAsync(purchase);

        await _handler.ExecuteAsync(new UpdateCreditCardPurchaseDetailsCommand(
            purchase.Id,
            "  Presente  ",
            personal.Id,
            momCondition.Id));

        Assert.Equal("Presente", purchase.Description);
        Assert.Equal(personal.Id, purchase.CategoryId);
        Assert.Equal(momCondition.Id, purchase.ConditionId);
        Assert.Equal(1, _purchases.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_KeepingArchivedCategory_AllowsUpdatingDescriptionAndCondition()
    {
        var shopping = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Eletrônicos");
        shopping.Archive();
        var othersCondition = PurchaseCondition.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Terceiros");
        await _categories.AddAsync(shopping);
        await _conditions.AddAsync(othersCondition);

        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(),
            _accountId,
            _cardId,
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(300.00m),
            3,
            1,
            CardBillingRule.Of(3, 12),
            SystemPurchaseConditions.OwnId,
            "Compra",
            shopping.Id);
        await _purchases.AddAsync(purchase);

        await _handler.ExecuteAsync(new UpdateCreditCardPurchaseDetailsCommand(
            purchase.Id, "Presente", shopping.Id, othersCondition.Id));

        Assert.Equal("Presente", purchase.Description);
        Assert.Equal(shopping.Id, purchase.CategoryId);
        Assert.Equal(othersCondition.Id, purchase.ConditionId);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_KeepingArchivedCondition_AllowsUpdatingDescriptionAndCategory()
    {
        var shopping = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Eletrônicos");
        var archivedCondition = PurchaseCondition.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Antiga");
        archivedCondition.Archive();
        await _categories.AddAsync(shopping);
        await _conditions.AddAsync(archivedCondition);

        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(),
            _accountId,
            _cardId,
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(300.00m),
            3,
            1,
            CardBillingRule.Of(3, 12),
            archivedCondition.Id,
            "Compra",
            shopping.Id);
        await _purchases.AddAsync(purchase);

        await _handler.ExecuteAsync(new UpdateCreditCardPurchaseDetailsCommand(
            purchase.Id, "Presente", shopping.Id, archivedCondition.Id));

        Assert.Equal("Presente", purchase.Description);
        Assert.Equal(archivedCondition.Id, purchase.ConditionId);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ReassigningToArchivedCategory_ThrowsDomainException()
    {
        var shopping = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Eletrônicos");
        var archived = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Antiga");
        archived.Archive();
        await _categories.AddAsync(shopping);
        await _categories.AddAsync(archived);

        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(),
            _accountId,
            _cardId,
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(300.00m),
            3,
            1,
            CardBillingRule.Of(3, 12),
            SystemPurchaseConditions.OwnId,
            "Compra",
            shopping.Id);
        await _purchases.AddAsync(purchase);

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new UpdateCreditCardPurchaseDetailsCommand(
                purchase.Id, "Presente", archived.Id, SystemPurchaseConditions.OwnId)));

        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ReassigningToArchivedCondition_ThrowsDomainException()
    {
        var shopping = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Eletrônicos");
        var archivedCondition = PurchaseCondition.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Antiga");
        archivedCondition.Archive();
        await _categories.AddAsync(shopping);
        await _conditions.AddAsync(archivedCondition);

        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(),
            _accountId,
            _cardId,
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(300.00m),
            3,
            1,
            CardBillingRule.Of(3, 12),
            SystemPurchaseConditions.OwnId,
            "Compra",
            shopping.Id);
        await _purchases.AddAsync(purchase);

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new UpdateCreditCardPurchaseDetailsCommand(
                purchase.Id, "Presente", shopping.Id, archivedCondition.Id)));

        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownOrOtherUserCondition_ThrowsNotFound()
    {
        var shopping = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Eletrônicos");
        var otherUserCondition = PurchaseCondition.CreateForUser(Guid.NewGuid(), Guid.NewGuid(), "Outro");
        await _categories.AddAsync(shopping);
        await _conditions.AddAsync(otherUserCondition);

        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(),
            _accountId,
            _cardId,
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(300.00m),
            3,
            1,
            CardBillingRule.Of(3, 12),
            SystemPurchaseConditions.OwnId,
            "Compra",
            shopping.Id);
        await _purchases.AddAsync(purchase);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new UpdateCreditCardPurchaseDetailsCommand(
                purchase.Id, "Presente", shopping.Id, otherUserCondition.Id)));

        Assert.Equal(nameof(PurchaseCondition), exception.ResourceName);
        Assert.Equal(otherUserCondition.Id, exception.ResourceId);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownPurchase_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new UpdateCreditCardPurchaseDetailsCommand(
                unknownId, "Compra", Guid.NewGuid(), SystemPurchaseConditions.OwnId)));

        Assert.Equal(nameof(CreditCardPurchase), exception.ResourceName);
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
        Assert.Throws<ArgumentNullException>(
            () => new UpdateCreditCardPurchaseDetailsHandler(null!, _categories, _conditions, _purchases, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new UpdateCreditCardPurchaseDetailsHandler(_accountAccess, null!, _conditions, _purchases, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new UpdateCreditCardPurchaseDetailsHandler(_accountAccess, _categories, null!, _purchases, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new UpdateCreditCardPurchaseDetailsHandler(_accountAccess, _categories, _conditions, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new UpdateCreditCardPurchaseDetailsHandler(_accountAccess, _categories, _conditions, _purchases, null!));
    }
}
