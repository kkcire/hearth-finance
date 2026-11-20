using Hearth.Application.CreditCardPurchases.RegisterCreditCardPurchase;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.CreditCardPurchases;

public class RegisterCreditCardPurchaseHandlerTests
{
    private static readonly DateOnly InitialDate = new(2026, 10, 1);

    private readonly InMemoryAccountRepository _accounts = new();
    private readonly InMemoryCreditCardPurchaseRepository _purchases = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RegisterCreditCardPurchaseHandler _handler;

    public RegisterCreditCardPurchaseHandlerTests()
    {
        _handler = new RegisterCreditCardPurchaseHandler(_accounts, _purchases, _unitOfWork);
    }

    private async Task<Account> SeedAccountAsync()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            "Conta principal",
            Money.FromDecimal(1000.00m),
            InitialDate,
            CardBillingRule.Of(3, 12));
        await _accounts.AddAsync(account);
        return account;
    }

    [Fact]
    public async Task ExecuteAsync_NewPurchase_UsesAccountBillingRuleAndPersists()
    {
        var account = await SeedAccountAsync();

        var id = await _handler.ExecuteAsync(new RegisterCreditCardPurchaseCommand(
            account.Id,
            new DateOnly(2026, 10, 10),
            300.00m,
            3,
            1,
            PurchaseConditionKind.Custom,
            "Ana",
            "  Tênis  ",
            ExpenseCategory.Shopping));

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);

        var saved = await _purchases.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(account.Id, saved.AccountId);
        Assert.Equal(account.BillingRule, saved.Rule);
        Assert.Equal(PurchaseCondition.Custom("Ana"), saved.Condition);
        Assert.Equal("Tênis", saved.Description);
    }

    [Fact]
    public async Task ExecuteAsync_NewPurchaseBeforeInitialBalanceDate_ThrowsDomainException()
    {
        var account = await SeedAccountAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new RegisterCreditCardPurchaseCommand(
                account.Id,
                InitialDate.AddDays(-1),
                100.00m,
                1,
                1,
                PurchaseConditionKind.Own,
                null,
                "Compra",
                ExpenseCategory.Shopping)));

        Assert.Empty(_purchases.Purchases);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_PurchaseInProgress_FirstCountingCycleClosesOnOrAfterInitialDate_IsAccepted()
    {
        // Bought 01/07/2026 (before closing day 3): installment 4 closes on 03/10/2026 >= 01/10/2026,
        // even though the cycle started on 03/09/2026.
        var account = await SeedAccountAsync();

        var id = await _handler.ExecuteAsync(new RegisterCreditCardPurchaseCommand(
            account.Id,
            new DateOnly(2026, 7, 1),
            600.00m,
            6,
            4,
            PurchaseConditionKind.Own,
            null,
            "Notebook",
            ExpenseCategory.Shopping));

        Assert.NotNull(await _purchases.GetByIdAsync(id));
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_PurchaseInProgress_FirstCountingCycleClosedBeforeInitialDate_ThrowsDomainException()
    {
        // Bought 01/07/2026: installment 3 closed on 03/09/2026 < 01/10/2026.
        var account = await SeedAccountAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new RegisterCreditCardPurchaseCommand(
                account.Id,
                new DateOnly(2026, 7, 1),
                600.00m,
                6,
                3,
                PurchaseConditionKind.Own,
                null,
                "Notebook",
                ExpenseCategory.Shopping)));

        Assert.Empty(_purchases.Purchases);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ConditionNameWithNonCustomKind_ThrowsArgumentException()
    {
        var account = await SeedAccountAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.ExecuteAsync(
            new RegisterCreditCardPurchaseCommand(
                account.Id,
                InitialDate,
                100.00m,
                1,
                1,
                PurchaseConditionKind.Own,
                "Ana",
                "Compra",
                ExpenseCategory.Shopping)));

        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new RegisterCreditCardPurchaseCommand(
                unknownId,
                InitialDate,
                100.00m,
                1,
                1,
                PurchaseConditionKind.Own,
                null,
                "Compra",
                ExpenseCategory.Shopping)));

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
        Assert.Throws<ArgumentNullException>(
            () => new RegisterCreditCardPurchaseHandler(null!, _purchases, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new RegisterCreditCardPurchaseHandler(_accounts, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new RegisterCreditCardPurchaseHandler(_accounts, _purchases, null!));
    }
}
