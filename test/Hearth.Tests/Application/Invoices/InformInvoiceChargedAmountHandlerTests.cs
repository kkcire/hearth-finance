using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Application.Invoices.InformInvoiceChargedAmount;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Invoices;

public class InformInvoiceChargedAmountHandlerTests
{
    private static readonly DateOnly InitialDate = new(2026, 10, 1);

    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryInvoiceRepository _invoices = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly InformInvoiceChargedAmountHandler _handler;

    public InformInvoiceChargedAmountHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new InformInvoiceChargedAmountHandler(_accountAccess, _invoices, _unitOfWork);
    }

    private async Task<Account> SeedAccountAsync()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta principal",
            Money.FromDecimal(1000.00m),
            InitialDate,
            CardBillingRule.Of(3, 12));
        await _accounts.AddAsync(account);
        return account;
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvoiceDoesNotExist_CreatesWithAccountRuleAndSetsChargedAmount()
    {
        var account = await SeedAccountAsync();

        var invoiceId = await _handler.ExecuteAsync(
            new InformInvoiceChargedAmountCommand(account.Id, 2026, 11, 520.50m));

        Assert.NotEqual(Guid.Empty, invoiceId);
        Assert.Equal(1, _invoices.AddCount);
        Assert.Equal(0, _invoices.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);

        var saved = await _invoices.GetByIdAsync(invoiceId);
        Assert.NotNull(saved);
        Assert.Equal(account.Id, saved.AccountId);
        Assert.Equal(BillingCycle.Closing(YearMonth.Of(2026, 11), account.BillingRule), saved.Cycle);
        Assert.Equal(Money.FromDecimal(520.50m), saved.ChargedAmount);
        Assert.False(saved.IsPaid);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvoiceAlreadyExistsForCycle_UpdatesSameInvoiceMaintainingUniqueness()
    {
        var account = await SeedAccountAsync();

        var firstId = await _handler.ExecuteAsync(
            new InformInvoiceChargedAmountCommand(account.Id, 2026, 11, 500.00m));
        var secondId = await _handler.ExecuteAsync(
            new InformInvoiceChargedAmountCommand(account.Id, 2026, 11, 530.75m));

        Assert.Equal(firstId, secondId);
        Assert.Single(_invoices.Invoices);
        Assert.Equal(1, _invoices.AddCount);
        Assert.Equal(1, _invoices.UpdateCount);
        Assert.Equal(2, _unitOfWork.SaveChangesCount);

        var saved = await _invoices.GetByIdAsync(firstId);
        Assert.Equal(Money.FromDecimal(530.75m), saved!.ChargedAmount);
    }

    [Fact]
    public async Task ExecuteAsync_CycleClosedBeforeInitialBalanceDate_IsAllowed()
    {
        // A September invoice may still be paid in October after the initial balance date.
        var account = await SeedAccountAsync();

        var invoiceId = await _handler.ExecuteAsync(
            new InformInvoiceChargedAmountCommand(account.Id, 2026, 9, 400.00m));

        Assert.NotNull(await _invoices.GetByIdAsync(invoiceId));
    }

    [Fact]
    public async Task ExecuteAsync_ZeroChargedAmount_IsAccepted()
    {
        var account = await SeedAccountAsync();

        var invoiceId = await _handler.ExecuteAsync(
            new InformInvoiceChargedAmountCommand(account.Id, 2026, 11, 0m));

        var saved = await _invoices.GetByIdAsync(invoiceId);
        Assert.Equal(Money.Zero, saved!.ChargedAmount);
    }

    [Fact]
    public async Task ExecuteAsync_NegativeChargedAmount_ThrowsAndDoesNotSave()
    {
        var account = await SeedAccountAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _handler.ExecuteAsync(
            new InformInvoiceChargedAmountCommand(account.Id, 2026, 11, -10.00m)));

        Assert.Empty(_invoices.Invoices);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingPaidInvoice_ThrowsDomainExceptionAndDoesNotSave()
    {
        var account = await SeedAccountAsync();
        var invoice = Invoice.Create(
            Guid.NewGuid(),
            account.Id,
            BillingCycle.Closing(YearMonth.Of(2026, 11), account.BillingRule));
        invoice.InformChargedAmount(Money.FromDecimal(500.00m));
        invoice.MarkAsPaid(new DateOnly(2026, 11, 12));
        await _invoices.AddAsync(invoice);

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new InformInvoiceChargedAmountCommand(account.Id, 2026, 11, 600.00m)));

        Assert.Equal(Money.FromDecimal(500.00m), invoice.ChargedAmount);
        Assert.Equal(0, _invoices.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new InformInvoiceChargedAmountCommand(unknownId, 2026, 11, 500.00m)));

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
            () => new InformInvoiceChargedAmountHandler(null!, _invoices, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new InformInvoiceChargedAmountHandler(_accountAccess, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new InformInvoiceChargedAmountHandler(_accountAccess, _invoices, null!));
    }
}
