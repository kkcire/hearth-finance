using Hearth.Application;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Application.Reports.GetMonthlySurplus;
using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hearth.Tests.Application.Reports;

public class GetMonthlySurplusHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryIncomeRepository _incomes = new();
    private readonly InMemoryDirectExpenseRepository _directExpenses = new();
    private readonly InMemoryFixedExpenseRepository _fixedExpenses = new();
    private readonly InMemoryCreditCardPurchaseRepository _purchases = new();
    private readonly FakeClock _clock = new(new DateOnly(2026, 11, 20));
    private readonly GetMonthlySurplusHandler _handler;

    public GetMonthlySurplusHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new GetMonthlySurplusHandler(
            _accountAccess,
            _incomes,
            _directExpenses,
            _fixedExpenses,
            _purchases,
            _clock);
    }

    private async Task<Account> SeedAccountAsync(decimal initialBalance = 2100.00m)
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta principal",
            Money.FromDecimal(initialBalance),
            new DateOnly(2026, 10, 1),
            isInitialBalanceConfirmed: true);
        await _accounts.AddAsync(account);
        return account;
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCascadeSeriesAndAverageMonthlyVariation()
    {
        var account = await SeedAccountAsync(2100.00m);

        var novSalary = Income.Create(
            Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 25), Money.FromDecimal(3000.00m), "Salário");
        var rent = FixedExpense.Create(
            Guid.NewGuid(), account.Id, YearMonth.Of(2026, 11), null, Money.FromDecimal(1200.00m), "Aluguel", SystemCategories.ServicesId);
        var direct = DirectExpense.Create(
            Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 10), Money.FromDecimal(100.00m), "Mercado", SystemCategories.FoodId);
        var cardPurchase = CreditCardPurchase.Create(
            Guid.NewGuid(), account.Id, Guid.NewGuid(), new DateOnly(2026, 11, 5), Money.FromDecimal(400.00m),
            1, 1, CardBillingRule.Of(3, 12), SystemPurchaseConditions.OwnId, "Fatura Dez", SystemCategories.ShoppingId);

        await _incomes.AddAsync(novSalary);
        await _fixedExpenses.AddAsync(rent);
        await _directExpenses.AddAsync(direct);
        await _purchases.AddAsync(cardPurchase);

        var dto = await _handler.ExecuteAsync(
            new GetMonthlySurplusQuery(account.Id, 2026, 11, PastMonths: 1, FutureMonths: 1));

        Assert.Equal(account.Id, dto.AccountId);
        Assert.Equal(new DateOnly(2026, 11, 20), dto.AsOfDate);
        Assert.Equal(3, dto.Series.Count);

        // Nov: cash = 2100 - 100 = 2000; expected income = 3000 -> available = 5000; fixed = 1200 -> surplus = 3800
        Assert.Equal(2026, dto.SelectedMonth.Year);
        Assert.Equal(11, dto.SelectedMonth.Month);
        Assert.True(dto.SelectedMonth.IsCurrentMonth);
        Assert.False(dto.SelectedMonth.IsHistorical);
        Assert.True(dto.SelectedMonth.DirectExpensesAreFutureOnly);
        Assert.False(dto.Series[0].DirectExpensesAreFutureOnly);
        Assert.False(dto.Series[2].DirectExpensesAreFutureOnly);
        Assert.Equal(2000.00m, dto.SelectedMonth.StartingCashBalance);
        Assert.Equal(3000.00m, dto.SelectedMonth.ExpectedIncomes);
        Assert.Equal(5000.00m, dto.SelectedMonth.TotalAvailable);
        Assert.Equal(0.00m, dto.SelectedMonth.CardInvoiceDue);
        Assert.Equal(1200.00m, dto.SelectedMonth.FixedExpenses);
        Assert.Equal(0.00m, dto.SelectedMonth.DirectExpenses);
        Assert.Equal(1200.00m, dto.SelectedMonth.TotalDiscounts);
        Assert.Equal(3800.00m, dto.SelectedMonth.ProjectedSurplus);
        Assert.Equal(1700.00m, dto.SelectedMonth.NetMonthlyVariation);

        // Dec: starts from 3800 - 400 (card due Dec 12) - 1200 (rent) = 2200 (variation = -1600)
        var dec = dto.Series[2];
        Assert.Equal(2026, dec.Year);
        Assert.Equal(12, dec.Month);
        Assert.Equal(3800.00m, dec.StartingCashBalance);
        Assert.Equal(400.00m, dec.CardInvoiceDue);
        Assert.Equal(1200.00m, dec.FixedExpenses);
        Assert.Equal(2200.00m, dec.ProjectedSurplus);
        Assert.Equal(-1600.00m, dec.NetMonthlyVariation);

        // Average across Oct (0), Nov (+1700), Dec (-1600) = 100 / 3 = 33.33
        Assert.Equal(33.33m, dto.AverageMonthlyVariation);
        Assert.Null(dto.ConditionId);
    }

    [Fact]
    public async Task ExecuteAsync_WithConditionId_KeepsStartingCashIntactAndFiltersCardInvoiceDue()
    {
        var account = await SeedAccountAsync(2000.00m);
        var othersDueBeforeToday = CreditCardPurchase.Create(
            Guid.NewGuid(), account.Id, Guid.NewGuid(), new DateOnly(2026, 10, 10), Money.FromDecimal(300.00m),
            1, 1, CardBillingRule.Of(3, 12), SystemPurchaseConditions.OthersId, "Fatura Nov Outros", SystemCategories.ShoppingId);
        var ownDueDec = CreditCardPurchase.Create(
            Guid.NewGuid(), account.Id, Guid.NewGuid(), new DateOnly(2026, 11, 5), Money.FromDecimal(400.00m),
            1, 1, CardBillingRule.Of(3, 12), SystemPurchaseConditions.OwnId, "Fatura Dez Própria", SystemCategories.ShoppingId);
        var othersDueDec = CreditCardPurchase.Create(
            Guid.NewGuid(), account.Id, Guid.NewGuid(), new DateOnly(2026, 11, 6), Money.FromDecimal(600.00m),
            1, 1, CardBillingRule.Of(3, 12), SystemPurchaseConditions.OthersId, "Fatura Dez Outros", SystemCategories.ShoppingId);

        await _purchases.AddAsync(othersDueBeforeToday);
        await _purchases.AddAsync(ownDueDec);
        await _purchases.AddAsync(othersDueDec);

        var filtered = await _handler.ExecuteAsync(new GetMonthlySurplusQuery(
            account.Id, 2026, 12, PastMonths: 0, FutureMonths: 1, ConditionId: SystemPurchaseConditions.OwnId));

        Assert.Equal(SystemPurchaseConditions.OwnId, filtered.ConditionId);
        // Nov RealBalance = 2000 - 300 (already due Nov 12 <= Nov 20) = 1700 (NEVER masked!)
        Assert.Equal(1700.00m, filtered.Series[0].StartingCashBalance);
        Assert.Equal(1700.00m, filtered.SelectedMonth.StartingCashBalance);
        // Dec CardInvoiceDue counts ONLY Own (400), NOT Others (600)
        Assert.Equal(400.00m, filtered.SelectedMonth.CardInvoiceDue);
        Assert.Equal(1300.00m, filtered.SelectedMonth.ProjectedSurplus);

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.ExecuteAsync(
            new GetMonthlySurplusQuery(account.Id, 2026, 12, ConditionId: Guid.Empty)));
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.ExecuteAsync(new GetMonthlySurplusQuery(unknownId, 2026, 11)));

        Assert.Equal(nameof(Account), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidMonthOrWindow_Throws()
    {
        var account = await SeedAccountAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _handler.ExecuteAsync(new GetMonthlySurplusQuery(account.Id, 2026, 0)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _handler.ExecuteAsync(new GetMonthlySurplusQuery(account.Id, 2026, 11, PastMonths: -1)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _handler.ExecuteAsync(new GetMonthlySurplusQuery(account.Id, 2026, 11, FutureMonths: -1)));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.ExecuteAsync(null!));
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GetMonthlySurplusHandler(null!, _incomes, _directExpenses, _fixedExpenses, _purchases, _clock));
        Assert.Throws<ArgumentNullException>(() =>
            new GetMonthlySurplusHandler(_accountAccess, null!, _directExpenses, _fixedExpenses, _purchases, _clock));
        Assert.Throws<ArgumentNullException>(() =>
            new GetMonthlySurplusHandler(_accountAccess, _incomes, null!, _fixedExpenses, _purchases, _clock));
        Assert.Throws<ArgumentNullException>(() =>
            new GetMonthlySurplusHandler(_accountAccess, _incomes, _directExpenses, null!, _purchases, _clock));
        Assert.Throws<ArgumentNullException>(() =>
            new GetMonthlySurplusHandler(_accountAccess, _incomes, _directExpenses, _fixedExpenses, null!, _clock));
        Assert.Throws<ArgumentNullException>(() =>
            new GetMonthlySurplusHandler(_accountAccess, _incomes, _directExpenses, _fixedExpenses, _purchases, null!));
    }

    [Fact]
    public void AddApplication_RegistersGetMonthlySurplusHandler()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(GetMonthlySurplusHandler));
    }
}
