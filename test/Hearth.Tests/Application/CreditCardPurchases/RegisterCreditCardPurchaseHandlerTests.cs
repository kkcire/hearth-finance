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
    private readonly FakeClock _clock = new(new DateOnly(2026, 10, 10));
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RegisterCreditCardPurchaseHandler _handler;

    public RegisterCreditCardPurchaseHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new RegisterCreditCardPurchaseHandler(
            _accountAccess, _creditCards, _categories, _purchases, _clock, _unitOfWork);
    }

    private async Task<(Account Account, CreditCard Card, Category Category)> SeedAccountCardAndCategoryAsync(
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

        var card = CreditCard.Create(
            Guid.NewGuid(),
            account.Id,
            rule ?? CardBillingRule.Of(3, 12),
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
    public async Task ExecuteAsync_PurchaseBeforeInitialBalanceDate_WithOpenDueDate_IsAccepted()
    {
        // Account created 23/11, today 23/11, card (25, 5): purchase on 08/11 closes 25/11 and is due 05/12.
        _clock.Today = new DateOnly(2026, 11, 23);
        var (account, card, category) = await SeedAccountCardAndCategoryAsync(
            initialDate: new DateOnly(2026, 11, 23),
            rule: CardBillingRule.Of(25, 5));

        var id = await _handler.ExecuteAsync(new RegisterCreditCardPurchaseCommand(
            account.Id,
            card.Id,
            new DateOnly(2026, 11, 8),
            250.00m,
            1,
            1,
            PurchaseConditionKind.Own,
            null,
            "Compra antes do saldo inicial",
            category.Id));

        var saved = await _purchases.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(new DateOnly(2026, 11, 8), saved.Date);
        Assert.Equal(new DateOnly(2026, 12, 5), saved.FirstActiveCycle.DueDate);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_FirstActiveDueDateBeforeInitialBalanceDate_ThrowsDomainException()
    {
        // Account created 23/11, card (25, 5): purchase on 08/10 -> closes 25/10, due 05/11 (< 23/11).
        _clock.Today = new DateOnly(2026, 11, 1);
        var (account, card, category) = await SeedAccountCardAndCategoryAsync(
            initialDate: new DateOnly(2026, 11, 23),
            rule: CardBillingRule.Of(25, 5));

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new RegisterCreditCardPurchaseCommand(
                account.Id,
                card.Id,
                new DateOnly(2026, 10, 8),
                100.00m,
                1,
                1,
                PurchaseConditionKind.Own,
                null,
                "Fatura antes do saldo inicial",
                category.Id)));

        Assert.Empty(_purchases.Purchases);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_FirstActiveDueDateBeforeToday_ThrowsDomainException()
    {
        // InitialDate = 01/10, today = 15/10, card (3, 12): purchase 01/10 -> closes 03/10, due 12/10 (< today 15/10).
        _clock.Today = new DateOnly(2026, 10, 15);
        var (account, card, category) = await SeedAccountCardAndCategoryAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new RegisterCreditCardPurchaseCommand(
                account.Id,
                card.Id,
                new DateOnly(2026, 10, 1),
                100.00m,
                1,
                1,
                PurchaseConditionKind.Own,
                null,
                "Ciclo vencido",
                category.Id)));

        Assert.Empty(_purchases.Purchases);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_OldTenInstallmentPurchaseWithInitialInstallment9_ValidatesInstallment9DueDateNotInstallment1()
    {
        // Account created on 2026-11-22, card (2, 12): purchase on 2026-03-02 in 10x with InitialInstallment = 9.
        // Installment 1 closes 2026-04-02 (due 2026-04-12 < InitialBalanceDate) -> previously paid, must NOT block!
        // Installment 9 closes 2026-12-02 (due 2026-12-12 >= 2026-11-22) -> accepted!
        _clock.Today = new DateOnly(2026, 11, 22);
        var (account, card, category) = await SeedAccountCardAndCategoryAsync(
            initialDate: new DateOnly(2026, 11, 22),
            rule: CardBillingRule.Of(2, 12));

        var id = await _handler.ExecuteAsync(new RegisterCreditCardPurchaseCommand(
            account.Id,
            card.Id,
            new DateOnly(2026, 3, 2),
            1000.00m,
            10,
            9,
            PurchaseConditionKind.Own,
            null,
            "Compra parcelada antiga",
            category.Id));

        var saved = await _purchases.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(new DateOnly(2026, 4, 12), saved.FirstCycle.DueDate);
        Assert.Equal(new DateOnly(2026, 12, 12), saved.FirstActiveCycle.DueDate);
        Assert.Equal(8, saved.GetInstallments().Count(i => i.IsPreviouslyPaid));
        Assert.Equal(2, saved.GetActiveInstallments().Count());
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_OldTenInstallmentPurchase_WhenInstallment9DueDateIsBeforeInitialBalanceDate_ThrowsIdentifyingInstallment9()
    {
        // With closing day 3 (card 3, 12), 2026-03-02 enters March's cycle (inst 1 due 2026-03-12),
        // so installment 9 is due 2026-11-12 (< InitialBalanceDate 2026-11-22) -> rejected with explicit 9/10 info;
        // advancing InitialInstallment to 10 (due 2026-12-12) succeeds!
        _clock.Today = new DateOnly(2026, 11, 22);
        var (account, card, category) = await SeedAccountCardAndCategoryAsync(
            initialDate: new DateOnly(2026, 11, 22),
            rule: CardBillingRule.Of(3, 12));

        var ex = await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new RegisterCreditCardPurchaseCommand(
                account.Id,
                card.Id,
                new DateOnly(2026, 3, 2),
                1000.00m,
                10,
                9,
                PurchaseConditionKind.Own,
                null,
                "Parcela 9 já vencida",
                category.Id)));

        Assert.Contains("9/10", ex.Message);
        Assert.Contains("2026-11-12", ex.Message);
        Assert.Empty(_purchases.Purchases);

        var id = await _handler.ExecuteAsync(new RegisterCreditCardPurchaseCommand(
            account.Id,
            card.Id,
            new DateOnly(2026, 3, 2),
            1000.00m,
            10,
            10,
            PurchaseConditionKind.Own,
            null,
            "Apenas parcela 10 em aberto",
            category.Id));

        var saved = await _purchases.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(new DateOnly(2026, 12, 12), saved.FirstActiveCycle.DueDate);
    }

    [Fact]
    public async Task ExecuteAsync_PurchaseInProgress_FirstActiveDueDateOnOrAfterInitialDateAndToday_IsAccepted()
    {
        // Purchase on 01/07 (6x, initial 4), card (3, 12) -> inst 4 closes 03/10, due 12/10 (>= InitialDate 01/10 and >= today 10/10).
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
    public async Task ExecuteAsync_PurchaseInProgress_FirstActiveDueDateBeforeInitialDate_ThrowsDomainException()
    {
        // Purchase on 01/07 (6x, initial 3), card (3, 12) -> inst 3 closes 03/09, due 12/09 (< InitialDate 01/10).
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
            () => new RegisterCreditCardPurchaseHandler(null!, _creditCards, _categories, _purchases, _clock, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new RegisterCreditCardPurchaseHandler(_accountAccess, null!, _categories, _purchases, _clock, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new RegisterCreditCardPurchaseHandler(_accountAccess, _creditCards, null!, _purchases, _clock, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new RegisterCreditCardPurchaseHandler(_accountAccess, _creditCards, _categories, null!, _clock, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new RegisterCreditCardPurchaseHandler(_accountAccess, _creditCards, _categories, _purchases, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new RegisterCreditCardPurchaseHandler(_accountAccess, _creditCards, _categories, _purchases, _clock, null!));
    }
}
