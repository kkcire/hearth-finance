using Hearth.Application.Exceptions;
using Hearth.Application.Reports.GetProjectedResult;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Reports;

public class GetProjectedResultHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly InMemoryIncomeRepository _incomes = new();
    private readonly InMemoryDirectExpenseRepository _directExpenses = new();
    private readonly InMemoryFixedExpenseRepository _fixedExpenses = new();
    private readonly InMemoryCreditCardPurchaseRepository _purchases = new();
    private readonly GetProjectedResultHandler _handler;

    public GetProjectedResultHandlerTests()
    {
        _handler = new GetProjectedResultHandler(
            _accounts,
            _incomes,
            _directExpenses,
            _fixedExpenses,
            _purchases);
    }

    private async Task<Account> SeedAccountAsync(string name = "Conta")
    {
        var account = Account.Create(
            Guid.NewGuid(),
            name,
            Money.Zero,
            new DateOnly(2026, 1, 1),
            CardBillingRule.Of(3, 12));
        await _accounts.AddAsync(account);
        return account;
    }

    [Fact]
    public async Task ExecuteAsync_CalculatesAccrualProjectedResultForRequestedMonth()
    {
        var account = await SeedAccountAsync("Minha");
        var otherAccount = await SeedAccountAsync("Outra");

        // Expected in November, received in December -> still counts in November.
        var salary = Income.Create(
            Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 28), Money.FromDecimal(5000.00m), "Salario");
        salary.MarkAsReceived(new DateOnly(2026, 12, 2));

        var otherIncome = Income.Create(
            Guid.NewGuid(), otherAccount.Id, new DateOnly(2026, 11, 5), Money.FromDecimal(9000.00m), "Outro");

        var direct = DirectExpense.Create(
            Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 15), Money.FromDecimal(150.75m), "Mercado", ExpenseCategory.Food);

        var rent = FixedExpense.Create(
            Guid.NewGuid(), account.Id, YearMonth.Of(2026, 1), null, Money.FromDecimal(1000.00m), "Aluguel", ExpenseCategory.Services);

        // Bought 10/10 with closing day 3 -> installment 1 (300.00) closes in November.
        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(),
            account.Id,
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(300.00m),
            1,
            1,
            account.BillingRule,
            PurchaseCondition.Others,
            "Compra",
            ExpenseCategory.Shopping);

        await _incomes.AddAsync(salary);
        await _incomes.AddAsync(otherIncome);
        await _directExpenses.AddAsync(direct);
        await _fixedExpenses.AddAsync(rent);
        await _purchases.AddAsync(purchase);

        var dto = await _handler.ExecuteAsync(new GetProjectedResultQuery(account.Id, 2026, 11));

        Assert.Equal(account.Id, dto.AccountId);
        Assert.Equal(2026, dto.Year);
        Assert.Equal(11, dto.Month);
        Assert.Equal(5000.00m, dto.Incomes);
        Assert.Equal(300.00m, dto.CardInvoice);
        Assert.Equal(150.75m, dto.DirectExpenses);
        Assert.Equal(1000.00m, dto.FixedExpenses);
        Assert.Equal(3549.25m, dto.Result);
    }

    [Fact]
    public async Task ExecuteAsync_AllowsQueryingFutureMonths()
    {
        var account = await SeedAccountAsync();
        var rent = FixedExpense.Create(
            Guid.NewGuid(), account.Id, YearMonth.Of(2026, 1), null, Money.FromDecimal(1200.00m), "Aluguel", ExpenseCategory.Services);
        await _fixedExpenses.AddAsync(rent);

        var dto = await _handler.ExecuteAsync(new GetProjectedResultQuery(account.Id, 2027, 6));

        Assert.Equal(2027, dto.Year);
        Assert.Equal(6, dto.Month);
        Assert.Equal(1200.00m, dto.FixedExpenses);
        Assert.Equal(-1200.00m, dto.Result);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.ExecuteAsync(new GetProjectedResultQuery(unknownId, 2026, 11)));

        Assert.Equal(nameof(Account), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidMonth_Throws()
    {
        var account = await SeedAccountAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _handler.ExecuteAsync(new GetProjectedResultQuery(account.Id, 2026, 0)));
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
            new GetProjectedResultHandler(null!, _incomes, _directExpenses, _fixedExpenses, _purchases));
        Assert.Throws<ArgumentNullException>(() =>
            new GetProjectedResultHandler(_accounts, null!, _directExpenses, _fixedExpenses, _purchases));
        Assert.Throws<ArgumentNullException>(() =>
            new GetProjectedResultHandler(_accounts, _incomes, null!, _fixedExpenses, _purchases));
        Assert.Throws<ArgumentNullException>(() =>
            new GetProjectedResultHandler(_accounts, _incomes, _directExpenses, null!, _purchases));
        Assert.Throws<ArgumentNullException>(() =>
            new GetProjectedResultHandler(_accounts, _incomes, _directExpenses, _fixedExpenses, null!));
    }
}
