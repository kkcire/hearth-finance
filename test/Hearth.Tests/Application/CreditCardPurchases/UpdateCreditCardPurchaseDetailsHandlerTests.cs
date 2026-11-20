using Hearth.Application.CreditCardPurchases.UpdateCreditCardPurchaseDetails;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.CreditCardPurchases;

public class UpdateCreditCardPurchaseDetailsHandlerTests
{
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly InMemoryCreditCardPurchaseRepository _purchases = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly UpdateCreditCardPurchaseDetailsHandler _handler;

    public UpdateCreditCardPurchaseDetailsHandlerTests()
    {
        _handler = new UpdateCreditCardPurchaseDetailsHandler(_categories, _purchases, _unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingPurchase_UpdatesDescriptionCategoryAndConditionAndSaves()
    {
        var accountId = Guid.NewGuid();
        var shopping = Category.Create(Guid.NewGuid(), accountId, "Compras");
        var personal = Category.Create(Guid.NewGuid(), accountId, "Pessoal");
        await _categories.AddAsync(shopping);
        await _categories.AddAsync(personal);

        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(),
            accountId,
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(300.00m),
            3,
            1,
            CardBillingRule.Of(3, 12),
            PurchaseCondition.Own,
            "Compra",
            shopping.Id);
        await _purchases.AddAsync(purchase);

        await _handler.ExecuteAsync(new UpdateCreditCardPurchaseDetailsCommand(
            purchase.Id,
            "  Presente  ",
            personal.Id,
            PurchaseConditionKind.Custom,
            "Mãe"));

        Assert.Equal("Presente", purchase.Description);
        Assert.Equal(personal.Id, purchase.CategoryId);
        Assert.Equal(PurchaseCondition.Custom("Mãe"), purchase.Condition);
        Assert.Equal(1, _purchases.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_KeepingArchivedCategory_AllowsUpdatingDescriptionAndCondition()
    {
        var accountId = Guid.NewGuid();
        var shopping = Category.Create(Guid.NewGuid(), accountId, "Compras");
        shopping.Archive();
        await _categories.AddAsync(shopping);

        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(),
            accountId,
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(300.00m),
            3,
            1,
            CardBillingRule.Of(3, 12),
            PurchaseCondition.Own,
            "Compra",
            shopping.Id);
        await _purchases.AddAsync(purchase);

        await _handler.ExecuteAsync(new UpdateCreditCardPurchaseDetailsCommand(
            purchase.Id, "Presente", shopping.Id, PurchaseConditionKind.Others, null));

        Assert.Equal("Presente", purchase.Description);
        Assert.Equal(shopping.Id, purchase.CategoryId);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ReassigningToArchivedCategory_ThrowsDomainException()
    {
        var accountId = Guid.NewGuid();
        var shopping = Category.Create(Guid.NewGuid(), accountId, "Compras");
        var archived = Category.Create(Guid.NewGuid(), accountId, "Antiga");
        archived.Archive();
        await _categories.AddAsync(shopping);
        await _categories.AddAsync(archived);

        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(),
            accountId,
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(300.00m),
            3,
            1,
            CardBillingRule.Of(3, 12),
            PurchaseCondition.Own,
            "Compra",
            shopping.Id);
        await _purchases.AddAsync(purchase);

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new UpdateCreditCardPurchaseDetailsCommand(
                purchase.Id, "Presente", archived.Id, PurchaseConditionKind.Own, null)));

        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownPurchase_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new UpdateCreditCardPurchaseDetailsCommand(
                unknownId, "Compra", Guid.NewGuid(), PurchaseConditionKind.Own, null)));

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
            () => new UpdateCreditCardPurchaseDetailsHandler(null!, _purchases, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new UpdateCreditCardPurchaseDetailsHandler(_categories, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new UpdateCreditCardPurchaseDetailsHandler(_categories, _purchases, null!));
    }
}
