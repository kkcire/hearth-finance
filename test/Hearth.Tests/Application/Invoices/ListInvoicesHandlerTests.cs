using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Application.Invoices.ListInvoices;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Invoices;

public class ListInvoicesHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryInvoiceRepository _invoices = new();
    private readonly ListInvoicesHandler _handler;

    public ListInvoicesHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new ListInvoicesHandler(_accountAccess, _invoices);
    }

    private async Task<Account> SeedAccountAsync(string name = "Conta")
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            name,
            Money.Zero,
            new DateOnly(2026, 1, 1),
            CardBillingRule.Of(3, 12));
        await _accounts.AddAsync(account);
        return account;
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsOnlyAccountInvoicesOrderedByClosingMonthDescending()
    {
        var account = await SeedAccountAsync("Minha");
        var otherAccount = await SeedAccountAsync("Outra");

        var october = Invoice.Create(
            Guid.NewGuid(), account.Id, BillingCycle.Closing(YearMonth.Of(2026, 10), account.BillingRule));
        october.InformChargedAmount(Money.FromDecimal(400.00m));
        october.MarkAsPaid(new DateOnly(2026, 10, 12));

        var november = Invoice.Create(
            Guid.NewGuid(), account.Id, BillingCycle.Closing(YearMonth.Of(2026, 11), account.BillingRule));
        november.InformChargedAmount(Money.FromDecimal(550.00m));

        var other = Invoice.Create(
            Guid.NewGuid(), otherAccount.Id, BillingCycle.Closing(YearMonth.Of(2026, 12), otherAccount.BillingRule));

        await _invoices.AddAsync(october);
        await _invoices.AddAsync(november);
        await _invoices.AddAsync(other);

        var result = await _handler.ExecuteAsync(new ListInvoicesQuery(account.Id));

        Assert.Equal(2, result.Count);
        Assert.Equal(november.Id, result[0].Id);
        Assert.Equal(11, result[0].ClosingMonth);
        Assert.Equal(550.00m, result[0].ChargedAmount);
        Assert.False(result[0].IsPaid);

        Assert.Equal(october.Id, result[1].Id);
        Assert.Equal(10, result[1].ClosingMonth);
        Assert.True(result[1].IsPaid);
        Assert.Equal(new DateOnly(2026, 10, 12), result[1].PaidOn);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.ExecuteAsync(new ListInvoicesQuery(unknownId)));

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
        Assert.Throws<ArgumentNullException>(() => new ListInvoicesHandler(null!, _invoices));
        Assert.Throws<ArgumentNullException>(() => new ListInvoicesHandler(_accountAccess, null!));
    }
}
