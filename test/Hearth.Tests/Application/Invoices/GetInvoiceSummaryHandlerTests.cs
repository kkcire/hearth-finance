using Hearth.Application.Authorization;
using Hearth.Application.Invoices.GetInvoiceSummary;
using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Invoices;

public sealed class GetInvoiceSummaryHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly InMemoryCreditCardRepository _creditCards = new();
    private readonly InMemoryCreditCardPurchaseRepository _purchases = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeClock _clock = new(new DateOnly(2026, 11, 10));
    private readonly GetInvoiceSummaryHandler _handler;

    public GetInvoiceSummaryHandlerTests()
    {
        var guard = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new GetInvoiceSummaryHandler(guard, _creditCards, _purchases, _clock);
    }

    private async Task<Account> SeedAccountAsync()
    {
        var account = Account.Create(
            Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Principal", Money.Zero, new DateOnly(2026, 1, 1), true);
        await _accounts.AddAsync(account);
        return account;
    }

    [Fact]
    public async Task ExecuteAsync_WithNoCardsAndNoInstallments_ReturnsEmptyWithoutCard()
    {
        var account = await SeedAccountAsync();

        var summary = await _handler.ExecuteAsync(new GetInvoiceSummaryQuery(account.Id, 2026, 11));

        Assert.False(summary.HasActiveCard);
        Assert.True(summary.IsEmptyWithoutCard);
        Assert.Equal(0m, summary.CalculatedAmount);
        Assert.Null(summary.ClosingDate);
        Assert.Null(summary.DueDate);
        Assert.Null(summary.Status);
        Assert.Equal(2026, summary.DefaultOpenClosingYear);
        Assert.Equal(11, summary.DefaultOpenClosingMonth);
        Assert.Empty(summary.Installments);
    }

    [Fact]
    public async Task ExecuteAsync_WithArchivedCardInstallmentsAndNoActiveCard_ShowsInstallmentsAndNotEmpty()
    {
        var account = await SeedAccountAsync();
        var archivedCard = CreditCard.Create(Guid.NewGuid(), account.Id, CardBillingRule.Of(3, 12), "Antigo", 1000m is var _ ? Money.FromDecimal(1000m) : null);
        archivedCard.Archive();
        await _creditCards.AddAsync(archivedCard);

        await _purchases.AddAsync(CreditCardPurchase.Create(
            Guid.NewGuid(), account.Id, archivedCard.Id, new DateOnly(2026, 10, 10), Money.FromDecimal(200m), 2, 1,
            CardBillingRule.Of(3, 12), PurchaseCondition.Own, "Parcelada", SystemCategories.ShoppingId));

        // Today is Nov 10: Nov 3 closed, Nov 12 due -> Closed
        var summary = await _handler.ExecuteAsync(new GetInvoiceSummaryQuery(account.Id, 2026, 11));

        Assert.False(summary.HasActiveCard);
        Assert.False(summary.IsEmptyWithoutCard);
        Assert.Equal(100m, summary.CalculatedAmount);
        Assert.Equal(new DateOnly(2026, 11, 3), summary.ClosingDate);
        Assert.Equal(new DateOnly(2026, 11, 12), summary.DueDate);
        Assert.Equal(InvoiceStatus.Closed, summary.Status);
        Assert.Single(summary.Installments);
    }

    [Fact]
    public async Task ExecuteAsync_WithActiveCard_ComputesDefaultOpenMonthAndTransitionsStatusToPaidOnDueDate()
    {
        var account = await SeedAccountAsync();
        var card = CreditCard.Create(Guid.NewGuid(), account.Id, CardBillingRule.Of(3, 12), "Nubank", Money.FromDecimal(5000m));
        await _creditCards.AddAsync(card);

        await _purchases.AddAsync(CreditCardPurchase.Create(
            Guid.NewGuid(), account.Id, card.Id, new DateOnly(2026, 10, 10), Money.FromDecimal(300m), 3, 1,
            card.BillingRule, PurchaseCondition.Own, "Compra", SystemCategories.ShoppingId));

        // On Nov 12 (after closing day 3): default open invoice is Dec 2026, and Nov 2026 invoice is Paid
        _clock.Today = new DateOnly(2026, 11, 12);
        var novSummary = await _handler.ExecuteAsync(new GetInvoiceSummaryQuery(account.Id, 2026, 11));

        Assert.True(novSummary.HasActiveCard);
        Assert.False(novSummary.IsEmptyWithoutCard);
        Assert.Equal(2026, novSummary.DefaultOpenClosingYear);
        Assert.Equal(12, novSummary.DefaultOpenClosingMonth);
        Assert.Equal(100m, novSummary.CalculatedAmount);
        Assert.Equal(InvoiceStatus.Paid, novSummary.Status);

        var decSummary = await _handler.ExecuteAsync(new GetInvoiceSummaryQuery(account.Id, 2026, 12));
        Assert.Equal(InvoiceStatus.Open, decSummary.Status);
    }
}
