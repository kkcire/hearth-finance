using Hearth.Application.CreditCardPurchases.UpdateCreditCardPurchaseDetails;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.CreditCardPurchases;

public class UpdateCreditCardPurchaseDetailsHandlerTests
{
    private readonly InMemoryCreditCardPurchaseRepository _purchases = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly UpdateCreditCardPurchaseDetailsHandler _handler;

    public UpdateCreditCardPurchaseDetailsHandlerTests()
    {
        _handler = new UpdateCreditCardPurchaseDetailsHandler(_purchases, _unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingPurchase_UpdatesDescriptionCategoryAndConditionAndSaves()
    {
        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(300.00m),
            3,
            1,
            CardBillingRule.Of(3, 12),
            PurchaseCondition.Own,
            "Compra",
            ExpenseCategory.Shopping);
        await _purchases.AddAsync(purchase);

        await _handler.ExecuteAsync(new UpdateCreditCardPurchaseDetailsCommand(
            purchase.Id,
            "  Presente  ",
            ExpenseCategory.Personal,
            PurchaseConditionKind.Custom,
            "Mãe"));

        Assert.Equal("Presente", purchase.Description);
        Assert.Equal(ExpenseCategory.Personal, purchase.Category);
        Assert.Equal(PurchaseCondition.Custom("Mãe"), purchase.Condition);
        Assert.Equal(1, _purchases.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownPurchase_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new UpdateCreditCardPurchaseDetailsCommand(
                unknownId, "Compra", ExpenseCategory.Shopping, PurchaseConditionKind.Own, null)));

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
            () => new UpdateCreditCardPurchaseDetailsHandler(null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new UpdateCreditCardPurchaseDetailsHandler(_purchases, null!));
    }
}
