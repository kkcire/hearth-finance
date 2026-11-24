using Hearth.Application.Authorization;
using Hearth.Application.CreditCards.ArchiveCreditCard;
using Hearth.Application.CreditCards.CreateCreditCard;
using Hearth.Application.CreditCards.DeleteCreditCard;
using Hearth.Application.CreditCards.GetActiveCreditCard;
using Hearth.Application.CreditCards.ListCreditCards;
using Hearth.Application.CreditCards.RestoreCreditCard;
using Hearth.Application.CreditCards.UpdateCreditCard;
using Hearth.Application.Exceptions;
using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.CreditCards;

public sealed class CreditCardHandlersTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly InMemoryCreditCardRepository _creditCards = new();
    private readonly InMemoryCreditCardPurchaseRepository _purchases = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeClock _clock = new(new DateOnly(2026, 11, 10));
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly AccountAccessGuard _guard;

    public CreditCardHandlersTests()
    {
        _guard = new AccountAccessGuard(_accounts, _currentUser);
    }

    private async Task<Account> SeedAccountAsync()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta Principal",
            Money.Zero,
            new DateOnly(2026, 1, 1),
            isInitialBalanceConfirmed: true);
        await _accounts.AddAsync(account);
        return account;
    }

    [Fact]
    public async Task CreateCreditCard_AllowsOneActiveCardPerAccount_AndAllowsAnotherAfterArchiving()
    {
        var account = await SeedAccountAsync();
        var create = new CreateCreditCardHandler(_guard, _creditCards, _unitOfWork);
        var archive = new ArchiveCreditCardHandler(_guard, _creditCards, _unitOfWork);

        var firstId = await create.ExecuteAsync(new CreateCreditCardCommand(account.Id, 3, 12, "Nubank", 5000m));

        // Second active card must fail
        await Assert.ThrowsAsync<DomainException>(() =>
            create.ExecuteAsync(new CreateCreditCardCommand(account.Id, 10, 20, "Inter", 3000m)));

        // Archive first card -> now creating a new active card succeeds
        Assert.True(await archive.ExecuteAsync(new ArchiveCreditCardCommand(firstId)));
        Assert.False(await archive.ExecuteAsync(new ArchiveCreditCardCommand(firstId))); // idempotent

        var secondId = await create.ExecuteAsync(new CreateCreditCardCommand(account.Id, 10, 20, "Inter", 3000m));
        Assert.NotEqual(firstId, secondId);
    }

    [Fact]
    public async Task DeleteCreditCard_WithoutPurchasesDeletes_WithPurchasesThrowsDomainException()
    {
        var account = await SeedAccountAsync();
        var create = new CreateCreditCardHandler(_guard, _creditCards, _unitOfWork);
        var delete = new DeleteCreditCardHandler(_guard, _creditCards, _purchases, _unitOfWork);

        var cardId = await create.ExecuteAsync(new CreateCreditCardCommand(account.Id, 3, 12, "Nubank", null));
        await delete.ExecuteAsync(new DeleteCreditCardCommand(cardId));
        Assert.Null(await _creditCards.GetByIdAsync(cardId));

        var secondCardId = await create.ExecuteAsync(new CreateCreditCardCommand(account.Id, 3, 12, "Inter", 2000m));
        await _purchases.AddAsync(CreditCardPurchase.Create(
            Guid.NewGuid(),
            account.Id,
            secondCardId,
            new DateOnly(2026, 11, 5),
            Money.FromDecimal(100m),
            1,
            1,
            CardBillingRule.Of(3, 12),
            SystemPurchaseConditions.OwnId,
            "Livro",
            SystemCategories.EducationId));

        await Assert.ThrowsAsync<DomainException>(() => delete.ExecuteAsync(new DeleteCreditCardCommand(secondCardId)));
        Assert.NotNull(await _creditCards.GetByIdAsync(secondCardId));
    }

    [Fact]
    public async Task UpdateAndGetActiveCreditCard_ComputesLiveUsedAndAvailableLimit()
    {
        var account = await SeedAccountAsync();
        var create = new CreateCreditCardHandler(_guard, _creditCards, _unitOfWork);
        var update = new UpdateCreditCardHandler(_guard, _creditCards, _unitOfWork);
        var getActive = new GetActiveCreditCardHandler(_guard, _creditCards, _purchases, _clock);
        var list = new ListCreditCardsHandler(_guard, _creditCards, _purchases, _clock);

        Assert.Null(await getActive.ExecuteAsync(new GetActiveCreditCardQuery(account.Id)));

        var cardId = await create.ExecuteAsync(new CreateCreditCardCommand(account.Id, 3, 12, "Nubank", 1000m));
        await update.ExecuteAsync(new UpdateCreditCardCommand(cardId, 5, 15, "Nubank Ultravioleta", 1500m));

        // Purchase on Nov 10 -> closes Dec 5, due Dec 15 (> today Nov 10) -> occupies 300 of limit
        await _purchases.AddAsync(CreditCardPurchase.Create(
            Guid.NewGuid(),
            account.Id,
            cardId,
            new DateOnly(2026, 11, 10),
            Money.FromDecimal(300m),
            3,
            1,
            CardBillingRule.Of(5, 15),
            SystemPurchaseConditions.OwnId,
            "Fone",
            SystemCategories.ShoppingId));

        var active = await getActive.ExecuteAsync(new GetActiveCreditCardQuery(account.Id));
        Assert.NotNull(active);
        Assert.Equal("Nubank Ultravioleta", active.Name);
        Assert.Equal(5, active.ClosingDay);
        Assert.Equal(15, active.DueDay);
        Assert.Equal(1500m, active.Limit);
        Assert.Equal(300m, active.UsedLimit);
        Assert.Equal(1200m, active.AvailableLimit);

        var listed = await list.ExecuteAsync(new ListCreditCardsQuery(account.Id));
        Assert.Single(listed);
    }

    [Fact]
    public async Task RestoreCreditCard_RestoresWhenNoActiveCardExists_BlocksWhenAnotherIsActive_AndListFiltersArchived()
    {
        var account = await SeedAccountAsync();
        var create = new CreateCreditCardHandler(_guard, _creditCards, _unitOfWork);
        var archive = new ArchiveCreditCardHandler(_guard, _creditCards, _unitOfWork);
        var restore = new RestoreCreditCardHandler(_guard, _creditCards, _unitOfWork);
        var list = new ListCreditCardsHandler(_guard, _creditCards, _purchases, _clock);

        var firstId = await create.ExecuteAsync(new CreateCreditCardCommand(account.Id, 3, 12, "Nubank", 5000m));

        // Restoring an already active card is idempotent
        Assert.False(await restore.ExecuteAsync(new RestoreCreditCardCommand(firstId)));

        // Archive first card -> active-only list is empty; includeArchived list still has it
        Assert.True(await archive.ExecuteAsync(new ArchiveCreditCardCommand(firstId)));
        Assert.Empty(await list.ExecuteAsync(new ListCreditCardsQuery(account.Id, IncludeArchived: false)));
        var allWithArchived = await list.ExecuteAsync(new ListCreditCardsQuery(account.Id, IncludeArchived: true));
        Assert.Single(allWithArchived);
        Assert.True(allWithArchived[0].IsArchived);

        // Restore succeeds when no active card exists
        Assert.True(await restore.ExecuteAsync(new RestoreCreditCardCommand(firstId)));
        Assert.Single(await list.ExecuteAsync(new ListCreditCardsQuery(account.Id, IncludeArchived: false)));

        // Archive first, create second active card -> restoring first must fail while second is active
        await archive.ExecuteAsync(new ArchiveCreditCardCommand(firstId));
        var secondId = await create.ExecuteAsync(new CreateCreditCardCommand(account.Id, 10, 20, "Inter", 3000m));

        var activeOnly = await list.ExecuteAsync(new ListCreditCardsQuery(account.Id, IncludeArchived: false));
        Assert.Single(activeOnly);
        Assert.Equal(secondId, activeOnly[0].Id);
        Assert.Equal(2, (await list.ExecuteAsync(new ListCreditCardsQuery(account.Id, IncludeArchived: true))).Count);

        await Assert.ThrowsAsync<DomainException>(() => restore.ExecuteAsync(new RestoreCreditCardCommand(firstId)));

        // Unknown card throws NotFoundException
        await Assert.ThrowsAsync<NotFoundException>(() => restore.ExecuteAsync(new RestoreCreditCardCommand(Guid.NewGuid())));
    }
}
