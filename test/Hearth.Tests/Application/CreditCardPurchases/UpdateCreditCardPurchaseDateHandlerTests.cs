using Hearth.Application.CreditCardPurchases.UpdateCreditCardPurchaseDate;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.CreditCardPurchases;

public class UpdateCreditCardPurchaseDateHandlerTests
{
    private static readonly DateOnly InitialDate = new(2026, 10, 1);

    private readonly InMemoryAccountRepository _accounts = new();
    private readonly InMemoryCreditCardPurchaseRepository _purchases = new();
    private readonly FakeClock _clock = new(new DateOnly(2026, 10, 20));
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly UpdateCreditCardPurchaseDateHandler _handler;

    public UpdateCreditCardPurchaseDateHandlerTests()
    {
        _handler = new UpdateCreditCardPurchaseDateHandler(_accounts, _purchases, _clock, _unitOfWork);
    }

    private async Task<CreditCardPurchase> SeedAsync(DateOnly? purchaseDate = null, int initialInstallment = 1)
    {
        var account = Account.Create(
            Guid.NewGuid(),
            "Conta principal",
            Money.FromDecimal(1000.00m),
            InitialDate,
            CardBillingRule.Of(3, 12));
        await _accounts.AddAsync(account);

        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(),
            account.Id,
            purchaseDate ?? new DateOnly(2026, 10, 10),
            Money.FromDecimal(600.00m),
            6,
            initialInstallment,
            account.BillingRule,
            PurchaseCondition.Own,
            "Compra",
            Guid.NewGuid());
        await _purchases.AddAsync(purchase);

        return purchase;
    }

    [Fact]
    public async Task ExecuteAsync_ValidNewDateBeforeClosing_UpdatesAndSaves()
    {
        var purchase = await SeedAsync();

        await _handler.ExecuteAsync(
            new UpdateCreditCardPurchaseDateCommand(purchase.Id, new DateOnly(2026, 10, 18)));

        Assert.Equal(new DateOnly(2026, 10, 18), purchase.Date);
        Assert.Equal(1, _purchases.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_NewDateBeforeInitialBalanceDate_ForNewPurchase_ThrowsDomainException()
    {
        var purchase = await SeedAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new UpdateCreditCardPurchaseDateCommand(purchase.Id, InitialDate.AddDays(-1))));

        Assert.Equal(new DateOnly(2026, 10, 10), purchase.Date);
        Assert.Equal(0, _purchases.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_PurchaseInProgress_NewDateMovesFirstCountingClosingBeforeInitialDate_Throws()
    {
        // Bought 10/07/2026, initial 4 -> installment 4 closes on 03/11/2026.
        // Moving to 01/06/2026 makes installment 4 close on 03/09/2026 < 01/10/2026.
        var purchase = await SeedAsync(purchaseDate: new DateOnly(2026, 7, 10), initialInstallment: 4);

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new UpdateCreditCardPurchaseDateCommand(purchase.Id, new DateOnly(2026, 6, 1))));

        Assert.Equal(new DateOnly(2026, 7, 10), purchase.Date);
        Assert.Equal(0, _purchases.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_AfterInvoiceCloses_ThrowsDomainExceptionAndDoesNotSave()
    {
        _clock.Today = new DateOnly(2026, 11, 3);
        var purchase = await SeedAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new UpdateCreditCardPurchaseDateCommand(purchase.Id, new DateOnly(2026, 10, 18))));

        Assert.Equal(0, _purchases.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_NewDateMovesIntoClosedCycle_ThrowsDomainExceptionAndDoesNotSave()
    {
        // Today is 20/10/2026, closing day 3 -> 02/10/2026 belongs to Oct cycle which closed on 03/10/2026.
        var purchase = await SeedAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new UpdateCreditCardPurchaseDateCommand(purchase.Id, new DateOnly(2026, 10, 2))));

        Assert.Equal(new DateOnly(2026, 10, 10), purchase.Date);
        Assert.Equal(0, _purchases.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownPurchase_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new UpdateCreditCardPurchaseDateCommand(unknownId, new DateOnly(2026, 10, 18))));

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
            () => new UpdateCreditCardPurchaseDateHandler(null!, _purchases, _clock, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new UpdateCreditCardPurchaseDateHandler(_accounts, null!, _clock, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new UpdateCreditCardPurchaseDateHandler(_accounts, _purchases, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new UpdateCreditCardPurchaseDateHandler(_accounts, _purchases, _clock, null!));
    }
}
