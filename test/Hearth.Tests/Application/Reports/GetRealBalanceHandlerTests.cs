using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Application.Reports.GetRealBalance;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Reports;

public class GetRealBalanceHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryIncomeRepository _incomes = new();
    private readonly InMemoryDirectExpenseRepository _directExpenses = new();
    private readonly InMemoryFixedExpenseRepository _fixedExpenses = new();
    private readonly InMemoryInvoiceRepository _invoices = new();
    private readonly FakeClock _clock = new(new DateOnly(2026, 11, 20));
    private readonly GetRealBalanceHandler _handler;

    public GetRealBalanceHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new GetRealBalanceHandler(
            _accountAccess,
            _incomes,
            _directExpenses,
            _fixedExpenses,
            _invoices,
            _clock);
    }

    private async Task<Account> SeedAccountAsync(
        decimal initialBalance = 1000.00m,
        DateOnly? initialDate = null)
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta",
            Money.FromDecimal(initialBalance),
            initialDate ?? new DateOnly(2026, 10, 1),
            CardBillingRule.Of(3, 12));
        await _accounts.AddAsync(account);
        return account;
    }

    [Fact]
    public async Task ExecuteAsync_WithNullUntil_UsesClockToday()
    {
        var account = await SeedAccountAsync(initialBalance: 1000.00m);

        var received = Income.Create(
            Guid.NewGuid(), account.Id, new DateOnly(2026, 10, 28), Money.FromDecimal(5000.00m), "Salario");
        received.MarkAsReceived(new DateOnly(2026, 11, 2));

        var futureReceived = Income.Create(
            Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 25), Money.FromDecimal(500.00m), "Bonus");
        futureReceived.MarkAsReceived(new DateOnly(2026, 11, 25));

        var direct = DirectExpense.Create(
            Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 10), Money.FromDecimal(200.00m), "Mercado", Guid.NewGuid());

        var rent = FixedExpense.Create(
            Guid.NewGuid(), account.Id, YearMonth.Of(2026, 10), null, Money.FromDecimal(1200.00m), "Aluguel", Guid.NewGuid());
        rent.MarkAsPaid(YearMonth.Of(2026, 11), new DateOnly(2026, 11, 5));

        var invoice = Invoice.Create(
            Guid.NewGuid(), account.Id, BillingCycle.Closing(YearMonth.Of(2026, 11), account.BillingRule));
        invoice.InformChargedAmount(Money.FromDecimal(450.50m));
        invoice.MarkAsPaid(new DateOnly(2026, 11, 12));

        await _incomes.AddAsync(received);
        await _incomes.AddAsync(futureReceived);
        await _directExpenses.AddAsync(direct);
        await _fixedExpenses.AddAsync(rent);
        await _invoices.AddAsync(invoice);

        var dto = await _handler.ExecuteAsync(new GetRealBalanceQuery(account.Id));

        Assert.Equal(account.Id, dto.AccountId);
        Assert.Equal(new DateOnly(2026, 11, 20), dto.Until);
        Assert.Equal(1000.00m, dto.InitialBalance);
        Assert.Equal(5000.00m, dto.Incomes);
        Assert.Equal(200.00m, dto.DirectExpenses);
        Assert.Equal(1200.00m, dto.FixedExpenses);
        Assert.Equal(450.50m, dto.CardInvoices);
        Assert.Equal(4149.50m, dto.Balance);
    }

    [Fact]
    public async Task ExecuteAsync_WithExplicitUntil_CalculatesBalanceOnRequestedDate()
    {
        var account = await SeedAccountAsync(initialBalance: 500.00m);

        var income = Income.Create(
            Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 5), Money.FromDecimal(300.00m), "Pix");
        income.MarkAsReceived(new DateOnly(2026, 11, 5));

        var laterExpense = DirectExpense.Create(
            Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 15), Money.FromDecimal(100.00m), "Jantar", Guid.NewGuid());

        await _incomes.AddAsync(income);
        await _directExpenses.AddAsync(laterExpense);

        var dto = await _handler.ExecuteAsync(
            new GetRealBalanceQuery(account.Id, new DateOnly(2026, 11, 10)));

        Assert.Equal(new DateOnly(2026, 11, 10), dto.Until);
        Assert.Equal(300.00m, dto.Incomes);
        Assert.Equal(0.00m, dto.DirectExpenses);
        Assert.Equal(800.00m, dto.Balance);
    }

    [Fact]
    public async Task ExecuteAsync_UntilBeforeInitialBalanceDate_DelegatesWithoutThrowing()
    {
        var account = await SeedAccountAsync(
            initialBalance: 750.00m,
            initialDate: new DateOnly(2026, 12, 1));

        var dto = await _handler.ExecuteAsync(
            new GetRealBalanceQuery(account.Id, new DateOnly(2026, 11, 15)));

        Assert.Equal(new DateOnly(2026, 11, 15), dto.Until);
        Assert.Equal(750.00m, dto.InitialBalance);
        Assert.Equal(0.00m, dto.Incomes);
        Assert.Equal(0.00m, dto.DirectExpenses);
        Assert.Equal(0.00m, dto.FixedExpenses);
        Assert.Equal(0.00m, dto.CardInvoices);
        Assert.Equal(750.00m, dto.Balance);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.ExecuteAsync(new GetRealBalanceQuery(unknownId)));

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
        Assert.Throws<ArgumentNullException>(() =>
            new GetRealBalanceHandler(null!, _incomes, _directExpenses, _fixedExpenses, _invoices, _clock));
        Assert.Throws<ArgumentNullException>(() =>
            new GetRealBalanceHandler(_accountAccess, null!, _directExpenses, _fixedExpenses, _invoices, _clock));
        Assert.Throws<ArgumentNullException>(() =>
            new GetRealBalanceHandler(_accountAccess, _incomes, null!, _fixedExpenses, _invoices, _clock));
        Assert.Throws<ArgumentNullException>(() =>
            new GetRealBalanceHandler(_accountAccess, _incomes, _directExpenses, null!, _invoices, _clock));
        Assert.Throws<ArgumentNullException>(() =>
            new GetRealBalanceHandler(_accountAccess, _incomes, _directExpenses, _fixedExpenses, null!, _clock));
        Assert.Throws<ArgumentNullException>(() =>
            new GetRealBalanceHandler(_accountAccess, _incomes, _directExpenses, _fixedExpenses, _invoices, null!));
    }
}
