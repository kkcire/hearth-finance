using Hearth.Application.Exceptions;
using Hearth.Application.Invoices.GetInvoiceSummary;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Invoices;

public class GetInvoiceSummaryHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly InMemoryInvoiceRepository _invoices = new();
    private readonly InMemoryCreditCardPurchaseRepository _purchases = new();
    private readonly GetInvoiceSummaryHandler _handler;

    public GetInvoiceSummaryHandlerTests()
    {
        _handler = new GetInvoiceSummaryHandler(_accounts, _invoices, _purchases);
    }

    private async Task<Account> SeedAccountAsync(string name = "Conta", CardBillingRule? rule = null)
    {
        var account = Account.Create(
            Guid.NewGuid(),
            name,
            Money.Zero,
            new DateOnly(2026, 1, 1),
            rule ?? CardBillingRule.Of(3, 12));
        await _accounts.AddAsync(account);
        return account;
    }

    [Fact]
    public async Task ExecuteAsync_WithPersistedInvoiceAndPurchases_ReturnsCalculatedChargedDivergenceAndInstallments()
    {
        var account = await SeedAccountAsync("Minha");
        var otherAccount = await SeedAccountAsync("Outra");

        // Closing in November 2026 (closing day 3): purchases from 03/10 to 02/11.
        var p1 = CreditCardPurchase.Create(
            Guid.NewGuid(),
            account.Id,
            new DateOnly(2026, 10, 15),
            Money.FromDecimal(100.00m),
            3,
            1,
            account.BillingRule,
            PurchaseCondition.Own,
            "Supermercado",
            ExpenseCategory.Food);

        var p2 = CreditCardPurchase.Create(
            Guid.NewGuid(),
            account.Id,
            new DateOnly(2026, 10, 5),
            Money.FromDecimal(200.00m),
            1,
            1,
            account.BillingRule,
            PurchaseCondition.Custom("Viagem"),
            "Passagem",
            ExpenseCategory.Transport);

        // Purchase in progress: 6x starting at installment 5 -> Nov is installment 4 (previously paid), ignored.
        var previouslyPaid = CreditCardPurchase.Create(
            Guid.NewGuid(),
            account.Id,
            new DateOnly(2026, 7, 10),
            Money.FromDecimal(600.00m),
            6,
            5,
            account.BillingRule,
            PurchaseCondition.Others,
            "Antiga",
            ExpenseCategory.Shopping);

        var otherPurchase = CreditCardPurchase.Create(
            Guid.NewGuid(),
            otherAccount.Id,
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(999.00m),
            1,
            1,
            otherAccount.BillingRule,
            PurchaseCondition.Own,
            "Outra conta",
            ExpenseCategory.Shopping);

        await _purchases.AddAsync(p1);
        await _purchases.AddAsync(p2);
        await _purchases.AddAsync(previouslyPaid);
        await _purchases.AddAsync(otherPurchase);

        var cycle = BillingCycle.Closing(YearMonth.Of(2026, 11), account.BillingRule);
        var invoice = Invoice.Create(Guid.NewGuid(), account.Id, cycle);
        invoice.InformChargedAmount(Money.FromDecimal(240.00m));
        invoice.MarkAsPaid(new DateOnly(2026, 11, 12));
        await _invoices.AddAsync(invoice);

        var summary = await _handler.ExecuteAsync(new GetInvoiceSummaryQuery(account.Id, 2026, 11));

        Assert.Equal(account.Id, summary.AccountId);
        Assert.Equal(invoice.Id, summary.InvoiceId);
        Assert.Equal(2026, summary.ClosingYear);
        Assert.Equal(11, summary.ClosingMonth);
        Assert.Equal(cycle.StartDate, summary.StartDate);
        Assert.Equal(cycle.EndDate, summary.EndDate);
        Assert.Equal(cycle.ClosingDate, summary.ClosingDate);
        Assert.Equal(cycle.DueDate, summary.DueDate);
        Assert.Equal(233.33m, summary.CalculatedAmount);
        Assert.Equal(240.00m, summary.ChargedAmount);
        Assert.Equal(6.67m, summary.Divergence);
        Assert.Equal(new DateOnly(2026, 11, 12), summary.PaidOn);
        Assert.True(summary.IsPaid);

        Assert.Equal(2, summary.Installments.Count);

        // Ordered by PurchaseDate ascending: 05/10 before 15/10.
        Assert.Equal(p2.Id, summary.Installments[0].PurchaseId);
        Assert.Equal(PurchaseConditionKind.Custom, summary.Installments[0].ConditionKind);
        Assert.Equal("Viagem", summary.Installments[0].ConditionName);
        Assert.Equal(1, summary.Installments[0].InstallmentNumber);
        Assert.Equal(1, summary.Installments[0].InstallmentCount);
        Assert.Equal(200.00m, summary.Installments[0].Amount);

        Assert.Equal(p1.Id, summary.Installments[1].PurchaseId);
        Assert.Equal(PurchaseConditionKind.Own, summary.Installments[1].ConditionKind);
        Assert.Null(summary.Installments[1].ConditionName);
        Assert.Equal(1, summary.Installments[1].InstallmentNumber);
        Assert.Equal(3, summary.Installments[1].InstallmentCount);
        Assert.Equal(33.33m, summary.Installments[1].Amount);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutPersistedInvoice_ReturnsVirtualCycleUsingAccountBillingRule()
    {
        var account = await SeedAccountAsync("Minha", CardBillingRule.Of(5, 15));
        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(),
            account.Id,
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(150.00m),
            1,
            1,
            account.BillingRule,
            PurchaseCondition.Others,
            "Farmacia",
            ExpenseCategory.Health);
        await _purchases.AddAsync(purchase);

        var expectedCycle = BillingCycle.Closing(YearMonth.Of(2026, 11), account.BillingRule);

        var summary = await _handler.ExecuteAsync(new GetInvoiceSummaryQuery(account.Id, 2026, 11));

        Assert.Null(summary.InvoiceId);
        Assert.Equal(expectedCycle.StartDate, summary.StartDate);
        Assert.Equal(expectedCycle.EndDate, summary.EndDate);
        Assert.Equal(expectedCycle.ClosingDate, summary.ClosingDate);
        Assert.Equal(expectedCycle.DueDate, summary.DueDate);
        Assert.Equal(150.00m, summary.CalculatedAmount);
        Assert.Null(summary.ChargedAmount);
        Assert.Null(summary.Divergence);
        Assert.Null(summary.PaidOn);
        Assert.False(summary.IsPaid);
        Assert.Single(summary.Installments);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesPersistedInvoiceCycleAfterAccountBillingRuleChanges()
    {
        var account = await SeedAccountAsync("Minha", CardBillingRule.Of(3, 12));
        var originalCycle = BillingCycle.Closing(YearMonth.Of(2026, 11), account.BillingRule);
        var invoice = Invoice.Create(Guid.NewGuid(), account.Id, originalCycle);
        invoice.InformChargedAmount(Money.FromDecimal(100.00m));
        await _invoices.AddAsync(invoice);

        account.ChangeBillingRule(CardBillingRule.Of(10, 20));
        await _accounts.UpdateAsync(account);

        var summary = await _handler.ExecuteAsync(new GetInvoiceSummaryQuery(account.Id, 2026, 11));

        Assert.Equal(originalCycle.ClosingDate, summary.ClosingDate);
        Assert.Equal(originalCycle.DueDate, summary.DueDate);
        Assert.Equal(100.00m, summary.Divergence);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.ExecuteAsync(new GetInvoiceSummaryQuery(unknownId, 2026, 11)));

        Assert.Equal(nameof(Account), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidMonth_Throws()
    {
        var account = await SeedAccountAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _handler.ExecuteAsync(new GetInvoiceSummaryQuery(account.Id, 2026, 13)));
    }

    [Fact]
    public async Task ExecuteAsync_NullQuery_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.ExecuteAsync(null!));
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GetInvoiceSummaryHandler(null!, _invoices, _purchases));
        Assert.Throws<ArgumentNullException>(
            () => new GetInvoiceSummaryHandler(_accounts, null!, _purchases));
        Assert.Throws<ArgumentNullException>(
            () => new GetInvoiceSummaryHandler(_accounts, _invoices, null!));
    }
}
