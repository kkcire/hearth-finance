using Hearth.Application.Authorization;
using Hearth.Application.CreditCardPurchases.UpdateCreditCardPurchaseDate;
using Hearth.Application.Exceptions;
using Hearth.Domain;
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
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryCreditCardPurchaseRepository _purchases = new();
    private readonly FakeClock _clock = new(new DateOnly(2026, 10, 20));
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly UpdateCreditCardPurchaseDateHandler _handler;

    public UpdateCreditCardPurchaseDateHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new UpdateCreditCardPurchaseDateHandler(_accountAccess, _purchases, _clock, _unitOfWork);
    }

    private async Task<CreditCardPurchase> SeedAsync(
        DateOnly? purchaseDate = null,
        int initialInstallment = 1,
        DateOnly? initialDate = null,
        CardBillingRule? rule = null)
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta principal",
            Money.FromDecimal(1000.00m),
            initialDate ?? InitialDate);
        await _accounts.AddAsync(account);

        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(),
            account.Id,
            Guid.NewGuid(),
            purchaseDate ?? new DateOnly(2026, 10, 10),
            Money.FromDecimal(600.00m),
            6,
            initialInstallment,
            rule ?? CardBillingRule.Of(3, 12),
            SystemPurchaseConditions.OwnId,
            "Compra",
            Guid.NewGuid());
        await _purchases.AddAsync(purchase);

        return purchase;
    }

    [Fact]
    public async Task ExecuteAsync_ValidNewDateBeforeInvoiceIsPaidIncludingWhileClosed_UpdatesAndSaves()
    {
        // Invoice closes on 03/11/2026 and is due on 12/11/2026 -> editable on 10/11/2026.
        _clock.Today = new DateOnly(2026, 11, 10);
        var purchase = await SeedAsync();

        await _handler.ExecuteAsync(
            new UpdateCreditCardPurchaseDateCommand(purchase.Id, new DateOnly(2026, 10, 18)));

        Assert.Equal(new DateOnly(2026, 10, 18), purchase.Date);
        Assert.Equal(1, _purchases.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_NewDateBeforeInitialBalanceDate_WithOpenDueDateAndOpenCycle_IsAccepted()
    {
        // Account created 23/11, today 23/11, card (25, 5): moving purchase from 23/11 to 08/11 keeps closing 25/11 and due 05/12.
        _clock.Today = new DateOnly(2026, 11, 23);
        var purchase = await SeedAsync(
            purchaseDate: new DateOnly(2026, 11, 23),
            initialInstallment: 1,
            initialDate: new DateOnly(2026, 11, 23),
            rule: CardBillingRule.Of(25, 5));

        await _handler.ExecuteAsync(
            new UpdateCreditCardPurchaseDateCommand(purchase.Id, new DateOnly(2026, 11, 8)));

        Assert.Equal(new DateOnly(2026, 11, 8), purchase.Date);
        Assert.Equal(new DateOnly(2026, 12, 5), purchase.FirstActiveCycle.DueDate);
        Assert.Equal(1, _purchases.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_NewDateMovesDueDateBeforeInitialBalanceDate_WhileCycleStillOpen_IsAccepted()
    {
        // Account initialDate = 2026-11-23, today = 2026-10-10, card (25, 5):
        // purchase on 2026-11-08 moved to 2026-10-15 (closes 25/10 > today, due 05/11 < InitialBalanceDate 23/11).
        _clock.Today = new DateOnly(2026, 10, 10);
        var purchase = await SeedAsync(
            purchaseDate: new DateOnly(2026, 11, 8),
            initialInstallment: 1,
            initialDate: new DateOnly(2026, 11, 23),
            rule: CardBillingRule.Of(25, 5));

        await _handler.ExecuteAsync(
            new UpdateCreditCardPurchaseDateCommand(purchase.Id, new DateOnly(2026, 10, 15)));

        Assert.Equal(new DateOnly(2026, 10, 15), purchase.Date);
        Assert.Equal(1, _purchases.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_PurchaseInProgress_NewDateMovesFirstActiveClosingIntoClosedCycle_Throws()
    {
        var purchase = await SeedAsync(purchaseDate: new DateOnly(2026, 7, 10), initialInstallment: 4);

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new UpdateCreditCardPurchaseDateCommand(purchase.Id, new DateOnly(2026, 6, 1))));

        Assert.Equal(new DateOnly(2026, 7, 10), purchase.Date);
        Assert.Equal(0, _purchases.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_OnOrAfterDueDate_ThrowsDomainExceptionAndDoesNotSave()
    {
        _clock.Today = new DateOnly(2026, 11, 12);
        var purchase = await SeedAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new UpdateCreditCardPurchaseDateCommand(purchase.Id, new DateOnly(2026, 10, 18))));

        Assert.Equal(0, _purchases.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_NewDateMovesIntoPaidInvoice_ThrowsDomainExceptionAndDoesNotSave()
    {
        // Today is 20/10/2026 -> October invoice was paid on 12/10/2026.
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
            () => new UpdateCreditCardPurchaseDateHandler(_accountAccess, null!, _clock, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new UpdateCreditCardPurchaseDateHandler(_accountAccess, _purchases, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new UpdateCreditCardPurchaseDateHandler(_accountAccess, _purchases, _clock, null!));
    }
}
