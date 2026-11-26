using Hearth.Application.Authorization;
using Hearth.Application.CreditCardPurchases.UpdateCreditCardPurchaseAmount;
using Hearth.Application.Exceptions;
using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.CreditCardPurchases;

public class UpdateCreditCardPurchaseAmountHandlerTests
{
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryCreditCardPurchaseRepository _purchases = new();
    private readonly FakeClock _clock = new(new DateOnly(2026, 10, 20));
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly UpdateCreditCardPurchaseAmountHandler _handler;

    public UpdateCreditCardPurchaseAmountHandlerTests()
    {
        _accounts.AddAsync(Account.Create(
            _accountId,
            FakeCurrentUser.DefaultUserId,
            "Conta",
            Money.Zero,
            TestTime.UtcOn(new DateOnly(2026, 1, 1)))).GetAwaiter().GetResult();
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new UpdateCreditCardPurchaseAmountHandler(_accountAccess, _purchases, _clock, _unitOfWork);
    }

    private async Task<CreditCardPurchase> SeedPurchaseAsync()
    {
        // Purchase on 10/10 with closing day 3 -> first invoice closes on 03/11/2026.
        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(),
            _accountId,
            Guid.NewGuid(),
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(300.00m),
            3,
            1,
            CardBillingRule.Of(3, 12),
            SystemPurchaseConditions.OwnId,
            "Compra",
            Guid.NewGuid());
        await _purchases.AddAsync(purchase);
        return purchase;
    }

    [Fact]
    public async Task ExecuteAsync_BeforeInvoiceIsPaidIncludingWhileClosed_UpdatesAmountAndSaves()
    {
        // Invoice closes on 03/11/2026 and is due on 12/11/2026 -> still editable on 10/11/2026.
        _clock.Today = new DateOnly(2026, 11, 10);
        var purchase = await SeedPurchaseAsync();

        await _handler.ExecuteAsync(new UpdateCreditCardPurchaseAmountCommand(purchase.Id, 450.00m));

        Assert.Equal(Money.FromDecimal(450.00m), purchase.Amount);
        Assert.Equal(1, _purchases.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_OnOrAfterDueDate_ThrowsDomainExceptionAndDoesNotSave()
    {
        _clock.Today = new DateOnly(2026, 11, 12);
        var purchase = await SeedPurchaseAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new UpdateCreditCardPurchaseAmountCommand(purchase.Id, 450.00m)));

        Assert.Equal(Money.FromDecimal(300.00m), purchase.Amount);
        Assert.Equal(0, _purchases.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownPurchase_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new UpdateCreditCardPurchaseAmountCommand(unknownId, 450.00m)));

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
            () => new UpdateCreditCardPurchaseAmountHandler(_accountAccess, null!, _clock, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new UpdateCreditCardPurchaseAmountHandler(_accountAccess, _purchases, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new UpdateCreditCardPurchaseAmountHandler(_accountAccess, _purchases, _clock, null!));
    }
}
