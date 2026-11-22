using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Application.Invoices.MarkInvoiceAsPaid;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Invoices;

public class MarkInvoiceAsPaidHandlerTests
{
    private static readonly DateOnly InitialDate = new(2026, 10, 1);

    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryInvoiceRepository _invoices = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly MarkInvoiceAsPaidHandler _handler;

    public MarkInvoiceAsPaidHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new MarkInvoiceAsPaidHandler(_accountAccess, _invoices, _unitOfWork);
    }

    private async Task<Invoice> SeedInvoiceAsync(decimal? chargedAmount = 500.00m)
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta principal",
            Money.FromDecimal(1000.00m),
            InitialDate,
            CardBillingRule.Of(3, 12));
        await _accounts.AddAsync(account);

        var invoice = Invoice.Create(
            Guid.NewGuid(),
            account.Id,
            BillingCycle.Closing(YearMonth.Of(2026, 10), account.BillingRule));

        if (chargedAmount.HasValue)
        {
            invoice.InformChargedAmount(Money.FromDecimal(chargedAmount.Value));
        }

        await _invoices.AddAsync(invoice);
        return invoice;
    }

    [Fact]
    public async Task ExecuteAsync_OpenInvoiceWithCharge_MarksAsPaidAndSaves()
    {
        var invoice = await SeedInvoiceAsync();

        var changed = await _handler.ExecuteAsync(
            new MarkInvoiceAsPaidCommand(invoice.Id, new DateOnly(2026, 10, 12)));

        Assert.True(changed);
        Assert.True(invoice.IsPaid);
        Assert.Equal(new DateOnly(2026, 10, 12), invoice.PaidOn);
        Assert.Equal(1, _invoices.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyPaid_IsIdempotentAndDoesNotSaveAgain()
    {
        var invoice = await SeedInvoiceAsync();
        invoice.MarkAsPaid(new DateOnly(2026, 10, 12));

        var changed = await _handler.ExecuteAsync(
            new MarkInvoiceAsPaidCommand(invoice.Id, new DateOnly(2026, 10, 20)));

        Assert.False(changed);
        Assert.Equal(new DateOnly(2026, 10, 12), invoice.PaidOn);
        Assert.Equal(0, _invoices.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_PaidOnBeforeInitialBalanceDate_ThrowsDomainExceptionAndDoesNotSave()
    {
        var invoice = await SeedInvoiceAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new MarkInvoiceAsPaidCommand(invoice.Id, InitialDate.AddDays(-1))));

        Assert.False(invoice.IsPaid);
        Assert.Equal(0, _invoices.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutChargedAmount_ThrowsDomainExceptionAndDoesNotSave()
    {
        var invoice = await SeedInvoiceAsync(chargedAmount: null);

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new MarkInvoiceAsPaidCommand(invoice.Id, new DateOnly(2026, 10, 12))));

        Assert.False(invoice.IsPaid);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownInvoice_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new MarkInvoiceAsPaidCommand(unknownId, new DateOnly(2026, 10, 12))));

        Assert.Equal(nameof(Invoice), exception.ResourceName);
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
            () => new MarkInvoiceAsPaidHandler(null!, _invoices, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new MarkInvoiceAsPaidHandler(_accountAccess, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new MarkInvoiceAsPaidHandler(_accountAccess, _invoices, null!));
    }
}
