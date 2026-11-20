using Hearth.Application.CreditCardPurchases.ListCreditCardPurchases;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.CreditCardPurchases;

public class ListCreditCardPurchasesHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly InMemoryCreditCardPurchaseRepository _purchases = new();
    private readonly FakeClock _clock = new(new DateOnly(2026, 10, 20));
    private readonly ListCreditCardPurchasesHandler _handler;

    public ListCreditCardPurchasesHandlerTests()
    {
        _handler = new ListCreditCardPurchasesHandler(_accounts, _purchases, _clock);
    }

    private async Task<Account> SeedAccountAsync(string name = "Conta")
    {
        var account = Account.Create(
            Guid.NewGuid(),
            name,
            Money.Zero,
            new DateOnly(2026, 1, 1),
            CardBillingRule.Of(3, 12));
        await _accounts.AddAsync(account);
        return account;
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsAccountPurchasesWithLockFlagAndInstallments()
    {
        var account = await SeedAccountAsync("Minha");
        var otherAccount = await SeedAccountAsync("Outra");

        // Bought 01/10 -> closes on 03/10 -> locked on 20/10.
        var locked = CreditCardPurchase.Create(
            Guid.NewGuid(),
            account.Id,
            new DateOnly(2026, 10, 1),
            Money.FromDecimal(100.00m),
            2,
            1,
            account.BillingRule,
            PurchaseCondition.Own,
            "Fechada",
            Guid.NewGuid());

        // Bought 10/10 -> closes on 03/11 -> open on 20/10.
        var open = CreditCardPurchase.Create(
            Guid.NewGuid(),
            account.Id,
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(100.00m),
            3,
            1,
            account.BillingRule,
            PurchaseCondition.Others,
            "Aberta",
            Guid.NewGuid());

        var other = CreditCardPurchase.Create(
            Guid.NewGuid(),
            otherAccount.Id,
            new DateOnly(2026, 10, 15),
            Money.FromDecimal(500.00m),
            1,
            1,
            otherAccount.BillingRule,
            PurchaseCondition.Own,
            "Outro",
            Guid.NewGuid());

        await _purchases.AddAsync(locked);
        await _purchases.AddAsync(open);
        await _purchases.AddAsync(other);

        var result = await _handler.ExecuteAsync(new ListCreditCardPurchasesQuery(account.Id));

        Assert.Equal(2, result.Count);
        Assert.Equal(open.Id, result[0].Id);
        Assert.False(result[0].IsLocked);
        Assert.Equal(3, result[0].Installments.Count);
        Assert.Equal(33.34m, result[0].Installments[2].Amount);

        Assert.Equal(locked.Id, result[1].Id);
        Assert.True(result[1].IsLocked);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.ExecuteAsync(new ListCreditCardPurchasesQuery(unknownId)));

        Assert.Equal(nameof(Account), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
    }

    [Fact]
    public async Task ExecuteAsync_NullQuery_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.ExecuteAsync(null!));
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ListCreditCardPurchasesHandler(null!, _purchases, _clock));
        Assert.Throws<ArgumentNullException>(
            () => new ListCreditCardPurchasesHandler(_accounts, null!, _clock));
        Assert.Throws<ArgumentNullException>(
            () => new ListCreditCardPurchasesHandler(_accounts, _purchases, null!));
    }
}
