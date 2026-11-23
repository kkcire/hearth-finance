using Hearth.Application.Authorization;
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
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryCreditCardRepository _creditCards = new();
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly InMemoryCreditCardPurchaseRepository _purchases = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RegisterCreditCardPurchaseHandler _handler;

    public RegisterCreditCardPurchaseHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new RegisterCreditCardPurchaseHandler(
            _accountAccess, _creditCards, _categories, _purchases, _unitOfWork);
    }

    private async Task<(Account Account, CreditCard Card, Category Category)> SeedAccountCardAndCategoryAsync()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta principal",
            Money.FromDecimal(1000.00m),
            InitialDate);
        await _accounts.AddAsync(account);

        var card = CreditCard.Create(
            Guid.NewGuid(),
            account.Id,
            CardBillingRule.Of(3, 12),
            "Nubank",
            Money.FromDecimal(5000.00m));
        await _creditCards.AddAsync(card);

        var category = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Eletrônicos");
        await _categories.AddAsync(category);

        return (account, card, category);
    }

    [Fact]
    public async Task ExecuteAsync_NewPurchase_SnapshotsCardBillingRuleAndPersists()
    {
        var (account, card, category) = await SeedAccountCardAndCategoryAsync();

        var id = await _handler.ExecuteAsync(new RegisterCreditCardPurchaseCommand(
            account.Id,
            card.Id,
            new DateOnly(2026, 10, 10),
            300.00m,
            3,
            1,
            PurchaseConditionKind.Custom,
            "Ana",
            "  Tênis  ",
            category.Id));

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);

        var saved = await _purchases.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(account.Id, saved.AccountId);
        Assert.Equal(card.Id, saved.CreditCardId);
        Assert.Equal(card.BillingRule, saved.Rule);
        Assert.Equal(PurchaseCondition.Custom("Ana"), saved.Condition);
        Assert.Equal("Tênis", saved.Description);
        Assert.Equal(category.Id, saved.CategoryId);
    }

    [Fact]
    public async Task ExecuteAsync_ArchivedCard_ThrowsDomainException()
    {
        var (account, card, category) = await SeedAccountCardAndCategoryAsync();
        card.Archive();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new RegisterCreditCardPurchaseCommand(
                account.Id,
                card.Id,
                new DateOnly(2026, 10, 10),
                100.00m,
                1,
                1,
                PurchaseConditionKind.Own,
                null,
                "Compra",
                category.Id)));

        Assert.Empty(_purchases.Purchases);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownCard_ThrowsNotFound()
    {
        var (account, _, category) = await SeedAccountCardAndCategoryAsync();
        var unknownCardId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new RegisterCreditCardPurchaseCommand(
                account.Id,
                unknownCardId,
                new DateOnly(2026, 10, 10),
                100.00m,
                1,
                1,
                PurchaseConditionKind.Own,
                null,
                "Compra",
                category.Id)));

        Assert.Equal(nameof(CreditCard), ex.ResourceName);
        Assert.Equal(unknownCardId, ex.ResourceId);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ArchivedCategory_ThrowsDomainException()
    {
        var (account, card, category) = await SeedAccountCardAndCategoryAsync();
        category.Archive();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new RegisterCreditCardPurchaseCommand(
                account.Id,
                card.Id,
                new DateOnly(2026, 10, 10),
                100.00m,
                1,
                1,
                PurchaseConditionKind.Own,
                null,
                "Compra",
                category.Id)));

        Assert.Empty(_purchases.Purchases);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_NewPurchaseBeforeInitialBalanceDate_ThrowsDomainException()
    {
        var (account, card, category) = await SeedAccountCardAndCategoryAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new RegisterCreditCardPurchaseCommand(
                account.Id,
                card.Id,
                InitialDate.AddDays(-1),
                100.00m,
                1,
                1,
                PurchaseConditionKind.Own,
                null,
                "Compra",
                category.Id)));

        Assert.Empty(_purchases.Purchases);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_PurchaseInProgress_FirstCountingCycleClosesOnOrAfterInitialDate_IsAccepted()
    {
        var (account, card, category) = await SeedAccountCardAndCategoryAsync();

        var id = await _handler.ExecuteAsync(new RegisterCreditCardPurchaseCommand(
            account.Id,
            card.Id,
            new DateOnly(2026, 7, 1),
            600.00m,
            6,
            4,
            PurchaseConditionKind.Own,
            null,
            "Notebook",
            category.Id));

        Assert.NotNull(await _purchases.GetByIdAsync(id));
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_PurchaseInProgress_FirstCountingCycleClosedBeforeInitialDate_ThrowsDomainException()
    {
        var (account, card, category) = await SeedAccountCardAndCategoryAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new RegisterCreditCardPurchaseCommand(
                account.Id,
                card.Id,
                new DateOnly(2026, 7, 1),
                600.00m,
                6,
                3,
                PurchaseConditionKind.Own,
                null,
                "Notebook",
                category.Id)));

        Assert.Empty(_purchases.Purchases);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ConditionNameWithNonCustomKind_ThrowsArgumentException()
    {
        var (account, card, category) = await SeedAccountCardAndCategoryAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.ExecuteAsync(
            new RegisterCreditCardPurchaseCommand(
                account.Id,
                card.Id,
                InitialDate,
                100.00m,
                1,
                1,
                PurchaseConditionKind.Own,
                "Ana",
                "Compra",
                category.Id)));

        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new RegisterCreditCardPurchaseCommand(
                unknownId,
                Guid.NewGuid(),
                InitialDate,
                100.00m,
                1,
                1,
                PurchaseConditionKind.Own,
                null,
                "Compra",
                Guid.NewGuid())));

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
            () => new RegisterCreditCardPurchaseHandler(null!, _creditCards, _categories, _purchases, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new RegisterCreditCardPurchaseHandler(_accountAccess, null!, _categories, _purchases, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new RegisterCreditCardPurchaseHandler(_accountAccess, _creditCards, null!, _purchases, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new RegisterCreditCardPurchaseHandler(_accountAccess, _creditCards, _categories, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new RegisterCreditCardPurchaseHandler(_accountAccess, _creditCards, _categories, _purchases, null!));
    }
}
