using Hearth.Application.Authorization;
using Hearth.Application.CreditCardPurchases.RegisterCreditCardPurchase;
using Hearth.Application.Exceptions;
using Hearth.Domain;
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
    private readonly InMemoryPurchaseConditionRepository _conditions = new();
    private readonly InMemoryCreditCardPurchaseRepository _purchases = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RegisterCreditCardPurchaseHandler _handler;

    public RegisterCreditCardPurchaseHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new RegisterCreditCardPurchaseHandler(
            _accountAccess, _creditCards, _categories, _conditions, _purchases, _unitOfWork);
    }

    private async Task<(Account Account, CreditCard Card, Category Category, PurchaseCondition Condition)> SeedAccountCardAndCategoryAsync(
        DateOnly? initialDate = null,
        CardBillingRule? rule = null)
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta principal",
            Money.FromDecimal(1000.00m),
            TestTime.UtcOn(initialDate ?? InitialDate));
        await _accounts.AddAsync(account);

        var card = CreditCard.Create(
            Guid.NewGuid(),
            account.Id,
            rule ?? CardBillingRule.Of(3, 12),
            "Nubank",
            Money.FromDecimal(5000.00m));
        await _creditCards.AddAsync(card);

        var category = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Eletrônicos");
        await _categories.AddAsync(category);

        var condition = PurchaseCondition.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Ana");
        await _conditions.AddAsync(condition);

        return (account, card, category, condition);
    }

    [Fact]
    public async Task ExecuteAsync_NewPurchase_SnapshotsCardBillingRuleAndPersists()
    {
        var (account, card, category, condition) = await SeedAccountCardAndCategoryAsync();

        var id = await _handler.ExecuteAsync(new RegisterCreditCardPurchaseCommand(
            account.Id,
            card.Id,
            new DateOnly(2026, 10, 10),
            300.00m,
            3,
            1,
            condition.Id,
            "  Tênis  ",
            category.Id));

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);

        var saved = await _purchases.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(account.Id, saved.AccountId);
        Assert.Equal(card.Id, saved.CreditCardId);
        Assert.Equal(card.BillingRule, saved.Rule);
        Assert.Equal(condition.Id, saved.ConditionId);
        Assert.Equal("Tênis", saved.Description);
        Assert.Equal(category.Id, saved.CategoryId);
    }

    [Fact]
    public async Task ExecuteAsync_ArchivedCard_ThrowsDomainException()
    {
        var (account, card, category, _) = await SeedAccountCardAndCategoryAsync();
        card.Archive();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new RegisterCreditCardPurchaseCommand(
                account.Id,
                card.Id,
                new DateOnly(2026, 10, 10),
                100.00m,
                1,
                1,
                SystemPurchaseConditions.OwnId,
                "Compra",
                category.Id)));

        Assert.Empty(_purchases.Purchases);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownCard_ThrowsNotFound()
    {
        var (account, _, category, _) = await SeedAccountCardAndCategoryAsync();
        var unknownCardId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new RegisterCreditCardPurchaseCommand(
                account.Id,
                unknownCardId,
                new DateOnly(2026, 10, 10),
                100.00m,
                1,
                1,
                SystemPurchaseConditions.OwnId,
                "Compra",
                category.Id)));

        Assert.Equal(nameof(CreditCard), ex.ResourceName);
        Assert.Equal(unknownCardId, ex.ResourceId);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ArchivedCategory_ThrowsDomainException()
    {
        var (account, card, category, _) = await SeedAccountCardAndCategoryAsync();
        category.Archive();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new RegisterCreditCardPurchaseCommand(
                account.Id,
                card.Id,
                new DateOnly(2026, 10, 10),
                100.00m,
                1,
                1,
                SystemPurchaseConditions.OwnId,
                "Compra",
                category.Id)));

        Assert.Empty(_purchases.Purchases);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ArchivedCondition_ThrowsDomainException()
    {
        var (account, card, category, condition) = await SeedAccountCardAndCategoryAsync();
        condition.Archive();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new RegisterCreditCardPurchaseCommand(
                account.Id,
                card.Id,
                new DateOnly(2026, 10, 10),
                100.00m,
                1,
                1,
                condition.Id,
                "Compra",
                category.Id)));

        Assert.Empty(_purchases.Purchases);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownOrOtherUserCondition_ThrowsNotFound()
    {
        var (account, card, category, _) = await SeedAccountCardAndCategoryAsync();
        var otherUserCondition = PurchaseCondition.CreateForUser(Guid.NewGuid(), Guid.NewGuid(), "Terceiro");
        await _conditions.AddAsync(otherUserCondition);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new RegisterCreditCardPurchaseCommand(
                account.Id,
                card.Id,
                new DateOnly(2026, 10, 10),
                100.00m,
                1,
                1,
                otherUserCondition.Id,
                "Compra",
                category.Id)));

        Assert.Equal(nameof(PurchaseCondition), ex.ResourceName);
        Assert.Equal(otherUserCondition.Id, ex.ResourceId);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_PurchaseBeforeInitialBalanceDate_WithOpenDueDate_IsAccepted()
    {
        var (account, card, category, _) = await SeedAccountCardAndCategoryAsync(
            initialDate: new DateOnly(2026, 11, 23),
            rule: CardBillingRule.Of(25, 5));

        var id = await _handler.ExecuteAsync(new RegisterCreditCardPurchaseCommand(
            account.Id,
            card.Id,
            new DateOnly(2026, 11, 8),
            250.00m,
            1,
            1,
            SystemPurchaseConditions.OwnId,
            "Compra antes do saldo inicial",
            category.Id));

        var saved = await _purchases.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(new DateOnly(2026, 11, 8), saved.Date);
        Assert.Equal(new DateOnly(2026, 12, 5), saved.FirstActiveCycle.DueDate);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_HistoricalPurchaseAlreadyDueBeforeInitialBalanceDate_IsAcceptedForInvoiceHistory()
    {
        // Account created 2026-11-22, card (3, 12): purchase on 2026-10-10 due 2026-11-12 (< InitialBalanceDate)
        // is freely allowed so historical invoices can be recorded without affecting cash balance or limit.
        var (account, card, category, _) = await SeedAccountCardAndCategoryAsync(
            initialDate: new DateOnly(2026, 11, 22),
            rule: CardBillingRule.Of(3, 12));

        var id = await _handler.ExecuteAsync(new RegisterCreditCardPurchaseCommand(
            account.Id,
            card.Id,
            new DateOnly(2026, 10, 10),
            500.00m,
            1,
            1,
            SystemPurchaseConditions.OwnId,
            "Fatura já paga",
            category.Id));

        var saved = await _purchases.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(new DateOnly(2026, 11, 12), saved.FirstActiveCycle.DueDate);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_OldTenInstallmentPurchase_IsAcceptedRegardlessOfInitialInstallmentAndInitialBalanceDate()
    {
        var (account, card, category, _) = await SeedAccountCardAndCategoryAsync(
            initialDate: new DateOnly(2026, 11, 22),
            rule: CardBillingRule.Of(3, 12));

        // Even when installment 9 falls on 2026-11-12 (< InitialBalanceDate), registration succeeds freely.
        var id = await _handler.ExecuteAsync(new RegisterCreditCardPurchaseCommand(
            account.Id,
            card.Id,
            new DateOnly(2026, 3, 2),
            1000.00m,
            10,
            9,
            SystemPurchaseConditions.OwnId,
            "Compra parcelada antiga",
            category.Id));

        var saved = await _purchases.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(new DateOnly(2026, 3, 12), saved.FirstCycle.DueDate);
        Assert.Equal(new DateOnly(2026, 11, 12), saved.FirstActiveCycle.DueDate);
        Assert.Equal(8, saved.GetInstallments().Count(i => i.IsPreviouslyPaid));
        Assert.Equal(2, saved.GetActiveInstallments().Count());
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
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
                SystemPurchaseConditions.OwnId,
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
            () => new RegisterCreditCardPurchaseHandler(null!, _creditCards, _categories, _conditions, _purchases, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new RegisterCreditCardPurchaseHandler(_accountAccess, null!, _categories, _conditions, _purchases, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new RegisterCreditCardPurchaseHandler(_accountAccess, _creditCards, null!, _conditions, _purchases, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new RegisterCreditCardPurchaseHandler(_accountAccess, _creditCards, _categories, null!, _purchases, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new RegisterCreditCardPurchaseHandler(_accountAccess, _creditCards, _categories, _conditions, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new RegisterCreditCardPurchaseHandler(_accountAccess, _creditCards, _categories, _conditions, _purchases, null!));
    }
}
