using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Application.Invoices.RevertInvoicePayment;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Invoices;

public class RevertInvoicePaymentHandlerTests
{
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryInvoiceRepository _invoices = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RevertInvoicePaymentHandler _handler;

    public RevertInvoicePaymentHandlerTests()
    {
        _accounts.AddAsync(Account.Create(
            _accountId,
            FakeCurrentUser.DefaultUserId,
            "Conta",
            Money.Zero,
            new DateOnly(2026, 1, 1),
            CardBillingRule.Of(3, 12))).GetAwaiter().GetResult();
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new RevertInvoicePaymentHandler(_accountAccess, _invoices, _unitOfWork);
    }

    private async Task<Invoice> SeedInvoiceAsync(bool paid)
    {
        var invoice = Invoice.Create(
            Guid.NewGuid(),
            _accountId,
            BillingCycle.Closing(YearMonth.Of(2026, 10), CardBillingRule.Of(3, 12)));
        invoice.InformChargedAmount(Money.FromDecimal(500.00m));

        if (paid)
        {
            invoice.MarkAsPaid(new DateOnly(2026, 10, 12));
        }

        await _invoices.AddAsync(invoice);
        return invoice;
    }

    [Fact]
    public async Task ExecuteAsync_PaidInvoice_RevertsAndSaves()
    {
        var invoice = await SeedInvoiceAsync(paid: true);

        var changed = await _handler.ExecuteAsync(new RevertInvoicePaymentCommand(invoice.Id));

        Assert.True(changed);
        Assert.False(invoice.IsPaid);
        Assert.Null(invoice.PaidOn);
        Assert.Equal(Money.FromDecimal(500.00m), invoice.ChargedAmount);
        Assert.Equal(1, _invoices.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_OpenInvoice_IsIdempotentAndDoesNotSave()
    {
        var invoice = await SeedInvoiceAsync(paid: false);

        var changed = await _handler.ExecuteAsync(new RevertInvoicePaymentCommand(invoice.Id));

        Assert.False(changed);
        Assert.Equal(0, _invoices.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownInvoice_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.ExecuteAsync(new RevertInvoicePaymentCommand(unknownId)));

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
        Assert.Throws<ArgumentNullException>(() => new RevertInvoicePaymentHandler(null!, _invoices, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new RevertInvoicePaymentHandler(_accountAccess, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new RevertInvoicePaymentHandler(_accountAccess, _invoices, null!));
    }
}
