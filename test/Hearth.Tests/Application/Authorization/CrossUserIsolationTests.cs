using Hearth.Application.Accounts.ChangeBillingRule;
using Hearth.Application.Accounts.GetAccount;
using Hearth.Application.Accounts.ListAccounts;
using Hearth.Application.Accounts.RenameAccount;
using Hearth.Application.Accounts.UpdateInitialBalance;
using Hearth.Application.Authorization;
using Hearth.Application.Categories.ArchiveCategory;
using Hearth.Application.Categories.CreateCategory;
using Hearth.Application.Categories.DeleteCategory;
using Hearth.Application.Categories.ListCategories;
using Hearth.Application.Categories.RenameCategory;
using Hearth.Application.Categories.RestoreCategory;
using Hearth.Application.CategoryBudgets.ChangeCategoryBudgetAmount;
using Hearth.Application.CategoryBudgets.CreateCategoryBudget;
using Hearth.Application.CategoryBudgets.DeleteCategoryBudget;
using Hearth.Application.CategoryBudgets.EndCategoryBudget;
using Hearth.Application.CategoryBudgets.ListCategoryBudgets;
using Hearth.Application.CreditCardPurchases.DeleteCreditCardPurchase;
using Hearth.Application.CreditCardPurchases.ListCreditCardPurchases;
using Hearth.Application.CreditCardPurchases.RegisterCreditCardPurchase;
using Hearth.Application.CreditCardPurchases.UpdateCreditCardPurchaseAmount;
using Hearth.Application.CreditCardPurchases.UpdateCreditCardPurchaseDate;
using Hearth.Application.CreditCardPurchases.UpdateCreditCardPurchaseDetails;
using Hearth.Application.DirectExpenses.DeleteDirectExpense;
using Hearth.Application.DirectExpenses.ListDirectExpenses;
using Hearth.Application.DirectExpenses.RegisterDirectExpense;
using Hearth.Application.DirectExpenses.UpdateDirectExpense;
using Hearth.Application.Exceptions;
using Hearth.Application.FixedExpenses.ChangeFixedExpenseAmount;
using Hearth.Application.FixedExpenses.DeleteFixedExpense;
using Hearth.Application.FixedExpenses.EndFixedExpense;
using Hearth.Application.FixedExpenses.ListFixedExpenses;
using Hearth.Application.FixedExpenses.MarkFixedExpenseAsPaid;
using Hearth.Application.FixedExpenses.RegisterFixedExpense;
using Hearth.Application.FixedExpenses.RevertFixedExpensePayment;
using Hearth.Application.FixedExpenses.UpdateFixedExpenseDetails;
using Hearth.Application.Incomes.DeleteIncome;
using Hearth.Application.Incomes.ListIncomes;
using Hearth.Application.Incomes.MarkIncomeAsReceived;
using Hearth.Application.Incomes.RegisterIncome;
using Hearth.Application.Incomes.RevertIncomeToExpected;
using Hearth.Application.Incomes.UpdateIncome;
using Hearth.Application.Invoices.GetInvoiceSummary;
using Hearth.Application.Invoices.InformInvoiceChargedAmount;
using Hearth.Application.Invoices.ListInvoices;
using Hearth.Application.Invoices.MarkInvoiceAsPaid;
using Hearth.Application.Invoices.RevertInvoicePayment;
using Hearth.Application.Reports.GetBudgetStatus;
using Hearth.Application.Reports.GetExpensesByCategory;
using Hearth.Application.Reports.GetProjectedResult;
using Hearth.Application.Reports.GetRealBalance;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Authorization;

/// <summary>
/// Verifies end-to-end tenant isolation: User B can never list, inspect, or mutate any
/// account or child entity belonging to User A across every Application handler group.
/// </summary>
public sealed class CrossUserIsolationTests
{
    private static readonly Guid UserAId = Guid.Parse("01900000-0000-7000-8000-00000000000a");
    private static readonly Guid UserBId = Guid.Parse("01900000-0000-7000-8000-00000000000b");

    private readonly InMemoryAccountRepository _accounts = new();
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly InMemoryCategoryBudgetRepository _budgets = new();
    private readonly InMemoryDirectExpenseRepository _directExpenses = new();
    private readonly InMemoryFixedExpenseRepository _fixedExpenses = new();
    private readonly InMemoryCreditCardPurchaseRepository _purchases = new();
    private readonly InMemoryIncomeRepository _incomes = new();
    private readonly InMemoryInvoiceRepository _invoices = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new(new DateOnly(2026, 11, 20));

    private async Task<(Account Account, Category Category, CategoryBudget Budget, DirectExpense Direct, FixedExpense Fixed, CreditCardPurchase Purchase, Income Income, Invoice Invoice)>
        SeedUserADataAsync()
    {
        var rule = CardBillingRule.Of(3, 12);
        var account = Account.Create(Guid.NewGuid(), UserAId, "Conta User A", Money.FromDecimal(1000m), new DateOnly(2026, 1, 1), rule);
        var category = Category.Create(Guid.NewGuid(), account.Id, "Alimentação");
        var budget = CategoryBudget.Create(Guid.NewGuid(), account.Id, category.Id, YearMonth.Of(2026, 10), null, Money.FromDecimal(500m));
        var direct = DirectExpense.Create(Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 5), Money.FromDecimal(80m), "Almoço", category.Id);
        var fixedExp = FixedExpense.Create(Guid.NewGuid(), account.Id, YearMonth.Of(2026, 10), null, Money.FromDecimal(100m), "Internet", category.Id);
        fixedExp.MarkAsPaid(YearMonth.Of(2026, 10), new DateOnly(2026, 10, 5));
        var purchase = CreditCardPurchase.Create(Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 10), Money.FromDecimal(200m), 2, 1, rule, PurchaseCondition.Own, "Tênis", category.Id);
        var income = Income.Create(Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 5), Money.FromDecimal(3000m), "Salário");
        income.MarkAsReceived(new DateOnly(2026, 11, 5));
        var invoice = Invoice.Create(Guid.NewGuid(), account.Id, BillingCycle.Closing(YearMonth.Of(2026, 11), rule));
        invoice.InformChargedAmount(Money.FromDecimal(200m));
        invoice.MarkAsPaid(new DateOnly(2026, 11, 12));

        await _accounts.AddAsync(account);
        await _categories.AddAsync(category);
        await _budgets.AddAsync(budget);
        await _directExpenses.AddAsync(direct);
        await _fixedExpenses.AddAsync(fixedExp);
        await _purchases.AddAsync(purchase);
        await _incomes.AddAsync(income);
        await _invoices.AddAsync(invoice);

        return (account, category, budget, direct, fixedExp, purchase, income, invoice);
    }

    [Fact]
    public async Task UserB_CannotReadOrMutateAnyResourceOwnedByUserA()
    {
        var (account, category, budget, direct, fixedExp, purchase, income, invoice) = await SeedUserADataAsync();
        var userBGuard = new AccountAccessGuard(_accounts, new FakeCurrentUser(UserBId, "User B", "b@hearth.app"));

        // Accounts
        Assert.Empty(await new ListAccountsHandler(userBGuard, _accounts).ExecuteAsync());
        await Assert.ThrowsAsync<NotFoundException>(() => new GetAccountHandler(userBGuard).ExecuteAsync(new GetAccountQuery(account.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => new RenameAccountHandler(userBGuard, _accounts, _unitOfWork).ExecuteAsync(new RenameAccountCommand(account.Id, "Hack")));
        await Assert.ThrowsAsync<NotFoundException>(() => new UpdateInitialBalanceHandler(userBGuard, _accounts, _unitOfWork).ExecuteAsync(new UpdateInitialBalanceCommand(account.Id, 0m)));
        await Assert.ThrowsAsync<NotFoundException>(() => new ChangeBillingRuleHandler(userBGuard, _accounts, _unitOfWork).ExecuteAsync(new ChangeBillingRuleCommand(account.Id, 10, 20)));

        // Categories
        await Assert.ThrowsAsync<NotFoundException>(() => new CreateCategoryHandler(userBGuard, _categories, _unitOfWork).ExecuteAsync(new CreateCategoryCommand(account.Id, "Nova")));
        await Assert.ThrowsAsync<NotFoundException>(() => new ListCategoriesHandler(userBGuard, _categories).ExecuteAsync(new ListCategoriesQuery(account.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => new RenameCategoryHandler(userBGuard, _categories, _unitOfWork).ExecuteAsync(new RenameCategoryCommand(category.Id, "Hack")));
        await Assert.ThrowsAsync<NotFoundException>(() => new ArchiveCategoryHandler(userBGuard, _categories, _unitOfWork).ExecuteAsync(new ArchiveCategoryCommand(category.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => new RestoreCategoryHandler(userBGuard, _categories, _unitOfWork).ExecuteAsync(new RestoreCategoryCommand(category.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => new DeleteCategoryHandler(userBGuard, _categories, _directExpenses, _fixedExpenses, _purchases, _budgets, _unitOfWork).ExecuteAsync(new DeleteCategoryCommand(category.Id)));

        // Budgets
        await Assert.ThrowsAsync<NotFoundException>(() => new CreateCategoryBudgetHandler(userBGuard, _categories, _budgets, _unitOfWork).ExecuteAsync(new CreateCategoryBudgetCommand(account.Id, category.Id, 2026, 12, null, null, 300m)));
        await Assert.ThrowsAsync<NotFoundException>(() => new ListCategoryBudgetsHandler(userBGuard, _categories, _budgets).ExecuteAsync(new ListCategoryBudgetsQuery(account.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => new ChangeCategoryBudgetAmountHandler(userBGuard, _budgets, _clock, _unitOfWork).ExecuteAsync(new ChangeCategoryBudgetAmountCommand(budget.Id, 999m)));
        await Assert.ThrowsAsync<NotFoundException>(() => new EndCategoryBudgetHandler(userBGuard, _budgets, _clock, _unitOfWork).ExecuteAsync(new EndCategoryBudgetCommand(budget.Id, 2026, 12)));
        await Assert.ThrowsAsync<NotFoundException>(() => new DeleteCategoryBudgetHandler(userBGuard, _budgets, _unitOfWork).ExecuteAsync(new DeleteCategoryBudgetCommand(budget.Id)));

        // Direct expenses
        await Assert.ThrowsAsync<NotFoundException>(() => new RegisterDirectExpenseHandler(userBGuard, _categories, _directExpenses, _unitOfWork).ExecuteAsync(new RegisterDirectExpenseCommand(account.Id, new DateOnly(2026, 11, 10), 10m, "Hack", category.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => new ListDirectExpensesHandler(userBGuard, _directExpenses).ExecuteAsync(new ListDirectExpensesQuery(account.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => new UpdateDirectExpenseHandler(userBGuard, _categories, _directExpenses, _unitOfWork).ExecuteAsync(new UpdateDirectExpenseCommand(direct.Id, new DateOnly(2026, 11, 10), 10m, "Hack", category.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => new DeleteDirectExpenseHandler(userBGuard, _directExpenses, _unitOfWork).ExecuteAsync(new DeleteDirectExpenseCommand(direct.Id)));

        // Fixed expenses
        await Assert.ThrowsAsync<NotFoundException>(() => new RegisterFixedExpenseHandler(userBGuard, _categories, _fixedExpenses, _unitOfWork).ExecuteAsync(new RegisterFixedExpenseCommand(account.Id, 2026, 11, null, null, 50m, "Hack", category.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => new ListFixedExpensesHandler(userBGuard, _fixedExpenses).ExecuteAsync(new ListFixedExpensesQuery(account.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => new UpdateFixedExpenseDetailsHandler(userBGuard, _categories, _fixedExpenses, _unitOfWork).ExecuteAsync(new UpdateFixedExpenseDetailsCommand(fixedExp.Id, "Hack", category.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => new ChangeFixedExpenseAmountHandler(userBGuard, _fixedExpenses, _clock, _unitOfWork).ExecuteAsync(new ChangeFixedExpenseAmountCommand(fixedExp.Id, 999m)));
        await Assert.ThrowsAsync<NotFoundException>(() => new EndFixedExpenseHandler(userBGuard, _fixedExpenses, _clock, _unitOfWork).ExecuteAsync(new EndFixedExpenseCommand(fixedExp.Id, 2026, 12)));
        await Assert.ThrowsAsync<NotFoundException>(() => new MarkFixedExpenseAsPaidHandler(userBGuard, _fixedExpenses, _unitOfWork).ExecuteAsync(new MarkFixedExpenseAsPaidCommand(fixedExp.Id, 2026, 11, new DateOnly(2026, 11, 5))));
        await Assert.ThrowsAsync<NotFoundException>(() => new RevertFixedExpensePaymentHandler(userBGuard, _fixedExpenses, _unitOfWork).ExecuteAsync(new RevertFixedExpensePaymentCommand(fixedExp.Id, 2026, 10)));
        await Assert.ThrowsAsync<NotFoundException>(() => new DeleteFixedExpenseHandler(userBGuard, _fixedExpenses, _unitOfWork).ExecuteAsync(new DeleteFixedExpenseCommand(fixedExp.Id)));

        // Credit card purchases
        await Assert.ThrowsAsync<NotFoundException>(() => new RegisterCreditCardPurchaseHandler(userBGuard, _categories, _purchases, _unitOfWork).ExecuteAsync(new RegisterCreditCardPurchaseCommand(account.Id, new DateOnly(2026, 11, 10), 50m, 1, 1, PurchaseConditionKind.Own, null, "Hack", category.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => new ListCreditCardPurchasesHandler(userBGuard, _purchases, _clock).ExecuteAsync(new ListCreditCardPurchasesQuery(account.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => new UpdateCreditCardPurchaseAmountHandler(userBGuard, _purchases, _clock, _unitOfWork).ExecuteAsync(new UpdateCreditCardPurchaseAmountCommand(purchase.Id, 999m)));
        await Assert.ThrowsAsync<NotFoundException>(() => new UpdateCreditCardPurchaseDateHandler(userBGuard, _purchases, _clock, _unitOfWork).ExecuteAsync(new UpdateCreditCardPurchaseDateCommand(purchase.Id, new DateOnly(2026, 11, 15))));
        await Assert.ThrowsAsync<NotFoundException>(() => new UpdateCreditCardPurchaseDetailsHandler(userBGuard, _categories, _purchases, _unitOfWork).ExecuteAsync(new UpdateCreditCardPurchaseDetailsCommand(purchase.Id, "Hack", category.Id, PurchaseConditionKind.Own, null)));
        await Assert.ThrowsAsync<NotFoundException>(() => new DeleteCreditCardPurchaseHandler(userBGuard, _purchases, _clock, _unitOfWork).ExecuteAsync(new DeleteCreditCardPurchaseCommand(purchase.Id)));

        // Incomes
        await Assert.ThrowsAsync<NotFoundException>(() => new RegisterIncomeHandler(userBGuard, _incomes, _unitOfWork).ExecuteAsync(new RegisterIncomeCommand(account.Id, new DateOnly(2026, 11, 10), 10m, "Hack", null)));
        await Assert.ThrowsAsync<NotFoundException>(() => new ListIncomesHandler(userBGuard, _incomes).ExecuteAsync(new ListIncomesQuery(account.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => new UpdateIncomeHandler(userBGuard, _incomes, _unitOfWork).ExecuteAsync(new UpdateIncomeCommand(income.Id, new DateOnly(2026, 11, 10), 10m, "Hack")));
        await Assert.ThrowsAsync<NotFoundException>(() => new MarkIncomeAsReceivedHandler(userBGuard, _incomes, _unitOfWork).ExecuteAsync(new MarkIncomeAsReceivedCommand(income.Id, new DateOnly(2026, 11, 10))));
        await Assert.ThrowsAsync<NotFoundException>(() => new RevertIncomeToExpectedHandler(userBGuard, _incomes, _unitOfWork).ExecuteAsync(new RevertIncomeToExpectedCommand(income.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => new DeleteIncomeHandler(userBGuard, _incomes, _unitOfWork).ExecuteAsync(new DeleteIncomeCommand(income.Id)));

        // Invoices
        await Assert.ThrowsAsync<NotFoundException>(() => new InformInvoiceChargedAmountHandler(userBGuard, _invoices, _unitOfWork).ExecuteAsync(new InformInvoiceChargedAmountCommand(account.Id, 2026, 12, 100m)));
        await Assert.ThrowsAsync<NotFoundException>(() => new ListInvoicesHandler(userBGuard, _invoices).ExecuteAsync(new ListInvoicesQuery(account.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => new GetInvoiceSummaryHandler(userBGuard, _invoices, _purchases).ExecuteAsync(new GetInvoiceSummaryQuery(account.Id, 2026, 11)));
        await Assert.ThrowsAsync<NotFoundException>(() => new MarkInvoiceAsPaidHandler(userBGuard, _invoices, _unitOfWork).ExecuteAsync(new MarkInvoiceAsPaidCommand(invoice.Id, new DateOnly(2026, 11, 12))));
        await Assert.ThrowsAsync<NotFoundException>(() => new RevertInvoicePaymentHandler(userBGuard, _invoices, _unitOfWork).ExecuteAsync(new RevertInvoicePaymentCommand(invoice.Id)));

        // Reports
        await Assert.ThrowsAsync<NotFoundException>(() => new GetProjectedResultHandler(userBGuard, _incomes, _directExpenses, _fixedExpenses, _purchases).ExecuteAsync(new GetProjectedResultQuery(account.Id, 2026, 11)));
        await Assert.ThrowsAsync<NotFoundException>(() => new GetRealBalanceHandler(userBGuard, _incomes, _directExpenses, _fixedExpenses, _invoices, _clock).ExecuteAsync(new GetRealBalanceQuery(account.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => new GetExpensesByCategoryHandler(userBGuard, _categories, _directExpenses, _fixedExpenses, _purchases).ExecuteAsync(new GetExpensesByCategoryQuery(account.Id, 2026, 11)));
        await Assert.ThrowsAsync<NotFoundException>(() => new GetBudgetStatusHandler(userBGuard, _categories, _budgets, _directExpenses, _fixedExpenses, _purchases).ExecuteAsync(new GetBudgetStatusQuery(account.Id, 2026, 11)));

        // Nothing was mutated
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }
}
