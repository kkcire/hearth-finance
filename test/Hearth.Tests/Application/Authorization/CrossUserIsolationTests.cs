using Hearth.Application.Accounts.ConfirmInitialBalance;
using Hearth.Application.Accounts.DeleteBalanceAdjustment;
using Hearth.Application.Accounts.GetAccount;
using Hearth.Application.Accounts.ListAccounts;
using Hearth.Application.Accounts.RenameAccount;
using Hearth.Application.Accounts.SetAccountBalance;
using Hearth.Application.Authorization;
using Hearth.Application.Categories.ArchiveCategory;
using Hearth.Application.Categories.DeleteCategory;
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
using Hearth.Application.CreditCards.ArchiveCreditCard;
using Hearth.Application.CreditCards.CreateCreditCard;
using Hearth.Application.CreditCards.DeleteCreditCard;
using Hearth.Application.CreditCards.ListCreditCards;
using Hearth.Application.CreditCards.RestoreCreditCard;
using Hearth.Application.CreditCards.UpdateCreditCard;
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
using Hearth.Application.FixedIncomes.ConfirmFixedIncomeReceipt;
using Hearth.Application.FixedIncomes.CreateFixedIncome;
using Hearth.Application.FixedIncomes.DeleteFixedIncome;
using Hearth.Application.FixedIncomes.ListFixedIncomes;
using Hearth.Application.FixedIncomes.RevertFixedIncomeReceipt;
using Hearth.Application.FixedIncomes.StopFixedIncome;
using Hearth.Application.FixedIncomes.UpdateFixedIncome;
using Hearth.Application.Incomes.DeleteIncome;
using Hearth.Application.Incomes.ListIncomes;
using Hearth.Application.Incomes.RegisterIncome;
using Hearth.Application.Incomes.UpdateIncome;
using Hearth.Application.Activities.ListActivities;
using Hearth.Application.Invoices.GetInvoiceSummary;
using Hearth.Application.PurchaseConditions.ListPurchaseConditions;
using Hearth.Application.Reports.GetMonthlyDashboard;
using Hearth.Domain;
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
    private readonly InMemoryPurchaseConditionRepository _conditions = new();
    private readonly InMemoryCategoryBudgetRepository _budgets = new();
    private readonly InMemoryDirectExpenseRepository _directExpenses = new();
    private readonly InMemoryFixedExpenseRepository _fixedExpenses = new();
    private readonly InMemoryCreditCardRepository _creditCards = new();
    private readonly InMemoryCreditCardPurchaseRepository _purchases = new();
    private readonly InMemoryIncomeRepository _incomes = new();
    private readonly InMemoryFixedIncomeRepository _fixedIncomes = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new(new DateOnly(2026, 11, 20));

    private async Task<(Account Account, Category Category, PurchaseCondition Condition, CategoryBudget Budget, DirectExpense Direct, FixedExpense Fixed, CreditCard Card, CreditCardPurchase Purchase, Income Income, FixedIncome FixedIncome)>
        SeedUserADataAsync()
    {
        var rule = CardBillingRule.Of(3, 12);
        var account = Account.Create(Guid.NewGuid(), UserAId, "Conta User A", Money.FromDecimal(1000m), new DateTimeOffset(2026, 1, 1, 15, 0, 0, TimeSpan.Zero));
        var category = Category.CreateForUser(Guid.NewGuid(), UserAId, "Faculdade");
        var condition = PurchaseCondition.CreateForUser(Guid.NewGuid(), UserAId, "Viagem User A");
        var budget = CategoryBudget.Create(Guid.NewGuid(), account.Id, category.Id, YearMonth.Of(2026, 10), null, Money.FromDecimal(500m));
        var direct = DirectExpense.Create(Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 5), Money.FromDecimal(80m), "Almoço", category.Id);
        var fixedExp = FixedExpense.Create(Guid.NewGuid(), account.Id, YearMonth.Of(2026, 10), null, Money.FromDecimal(100m), "Internet", category.Id);
        fixedExp.MarkAsPaid(YearMonth.Of(2026, 10), new DateOnly(2026, 10, 5), _clock.Today);
        var card = CreditCard.Create(Guid.NewGuid(), account.Id, rule, "Nubank", Money.FromDecimal(3000m));
        var purchase = CreditCardPurchase.Create(Guid.NewGuid(), account.Id, card.Id, new DateOnly(2026, 11, 10), Money.FromDecimal(200m), 2, 1, rule, condition.Id, "Tênis", category.Id);
        var income = Income.Create(Guid.NewGuid(), account.Id, new DateOnly(2026, 11, 5), Money.FromDecimal(3000m), "Salário");
        var fixedIncome = FixedIncome.Create(Guid.NewGuid(), account.Id, YearMonth.Of(2026, 10), null, 5, Money.FromDecimal(4000m), "Salário Fixo");

        await _accounts.AddAsync(account);
        await _categories.AddAsync(category);
        await _conditions.AddAsync(condition);
        await _budgets.AddAsync(budget);
        await _directExpenses.AddAsync(direct);
        await _fixedExpenses.AddAsync(fixedExp);
        await _creditCards.AddAsync(card);
        await _purchases.AddAsync(purchase);
        await _incomes.AddAsync(income);
        await _fixedIncomes.AddAsync(fixedIncome);

        return (account, category, condition, budget, direct, fixedExp, card, purchase, income, fixedIncome);
    }

    [Fact]
    public async Task UserB_CannotReadOrMutateAnyResourceOwnedByUserA()
    {
        var (account, category, condition, budget, direct, fixedExp, card, purchase, income, fixedIncome) = await SeedUserADataAsync();
        var userBGuard = new AccountAccessGuard(_accounts, new FakeCurrentUser(UserBId, "User B", "b@hearth.app"));

        // Accounts
        Assert.Empty(await new ListAccountsHandler(userBGuard, _accounts).ExecuteAsync());
        await Assert.ThrowsAsync<NotFoundException>(() => new GetAccountHandler(userBGuard).ExecuteAsync(new GetAccountQuery(account.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => new RenameAccountHandler(userBGuard, _accounts, _unitOfWork).ExecuteAsync(new RenameAccountCommand(account.Id, "Hack")));
        await Assert.ThrowsAsync<NotFoundException>(() => new SetAccountBalanceHandler(userBGuard, _accounts, _clock, _unitOfWork).ExecuteAsync(new SetAccountBalanceCommand(account.Id, 500m)));
        await Assert.ThrowsAsync<NotFoundException>(() => new DeleteBalanceAdjustmentHandler(userBGuard, _accounts, _unitOfWork).ExecuteAsync(new DeleteBalanceAdjustmentCommand(account.Id, Guid.NewGuid())));
        await Assert.ThrowsAsync<NotFoundException>(() => new ConfirmInitialBalanceHandler(userBGuard, _accounts, _unitOfWork).ExecuteAsync(new ConfirmInitialBalanceCommand(account.Id, 0m)));

        // Categories (custom category owned by User A)
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
        await Assert.ThrowsAsync<NotFoundException>(() => new MarkFixedExpenseAsPaidHandler(userBGuard, _fixedExpenses, _clock, _unitOfWork).ExecuteAsync(new MarkFixedExpenseAsPaidCommand(fixedExp.Id, 2026, 11, new DateOnly(2026, 11, 5))));
        await Assert.ThrowsAsync<NotFoundException>(() => new RevertFixedExpensePaymentHandler(userBGuard, _fixedExpenses, _unitOfWork).ExecuteAsync(new RevertFixedExpensePaymentCommand(fixedExp.Id, 2026, 10)));
        await Assert.ThrowsAsync<NotFoundException>(() => new DeleteFixedExpenseHandler(userBGuard, _fixedExpenses, _unitOfWork).ExecuteAsync(new DeleteFixedExpenseCommand(fixedExp.Id)));

        // Credit cards
        await Assert.ThrowsAsync<NotFoundException>(() => new CreateCreditCardHandler(userBGuard, _creditCards, _unitOfWork).ExecuteAsync(new CreateCreditCardCommand(account.Id, 5, 12, "Hack", 1000m)));
        await Assert.ThrowsAsync<NotFoundException>(() => new ListCreditCardsHandler(userBGuard, _creditCards, _purchases, _clock).ExecuteAsync(new ListCreditCardsQuery(account.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => new UpdateCreditCardHandler(userBGuard, _creditCards, _unitOfWork).ExecuteAsync(new UpdateCreditCardCommand(card.Id, 5, 12, "Hack", 1000m)));
        await Assert.ThrowsAsync<NotFoundException>(() => new ArchiveCreditCardHandler(userBGuard, _creditCards, _unitOfWork).ExecuteAsync(new ArchiveCreditCardCommand(card.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => new RestoreCreditCardHandler(userBGuard, _creditCards, _unitOfWork).ExecuteAsync(new RestoreCreditCardCommand(card.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => new DeleteCreditCardHandler(userBGuard, _creditCards, _purchases, _unitOfWork).ExecuteAsync(new DeleteCreditCardCommand(card.Id)));

        // Purchase conditions (User B only sees system conditions, never User A's custom condition)
        var userBConditions = await new ListPurchaseConditionsHandler(userBGuard, _conditions).ExecuteAsync(new ListPurchaseConditionsQuery(IncludeArchived: true));
        Assert.DoesNotContain(userBConditions, c => c.Id == condition.Id);

        // Credit card purchases
        await Assert.ThrowsAsync<NotFoundException>(() => new RegisterCreditCardPurchaseHandler(userBGuard, _creditCards, _categories, _conditions, _purchases, _unitOfWork).ExecuteAsync(new RegisterCreditCardPurchaseCommand(account.Id, card.Id, new DateOnly(2026, 11, 10), 50m, 1, 1, SystemPurchaseConditions.OwnId, "Hack", category.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => new ListCreditCardPurchasesHandler(userBGuard, _purchases, _clock).ExecuteAsync(new ListCreditCardPurchasesQuery(account.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => new UpdateCreditCardPurchaseAmountHandler(userBGuard, _purchases, _clock, _unitOfWork).ExecuteAsync(new UpdateCreditCardPurchaseAmountCommand(purchase.Id, 999m)));
        await Assert.ThrowsAsync<NotFoundException>(() => new UpdateCreditCardPurchaseDateHandler(userBGuard, _purchases, _clock, _unitOfWork).ExecuteAsync(new UpdateCreditCardPurchaseDateCommand(purchase.Id, new DateOnly(2026, 11, 15))));
        await Assert.ThrowsAsync<NotFoundException>(() => new UpdateCreditCardPurchaseDetailsHandler(userBGuard, _categories, _conditions, _purchases, _unitOfWork).ExecuteAsync(new UpdateCreditCardPurchaseDetailsCommand(purchase.Id, "Hack", category.Id, SystemPurchaseConditions.OwnId)));
        await Assert.ThrowsAsync<NotFoundException>(() => new DeleteCreditCardPurchaseHandler(userBGuard, _purchases, _clock, _unitOfWork).ExecuteAsync(new DeleteCreditCardPurchaseCommand(purchase.Id)));

        // Incomes
        await Assert.ThrowsAsync<NotFoundException>(() => new RegisterIncomeHandler(userBGuard, _incomes, _conditions, _unitOfWork).ExecuteAsync(new RegisterIncomeCommand(account.Id, new DateOnly(2026, 11, 10), 10m, "Hack")));
        await Assert.ThrowsAsync<NotFoundException>(() => new ListIncomesHandler(userBGuard, _incomes, _clock).ExecuteAsync(new ListIncomesQuery(account.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => new UpdateIncomeHandler(userBGuard, _incomes, _conditions, _unitOfWork).ExecuteAsync(new UpdateIncomeCommand(income.Id, new DateOnly(2026, 11, 10), 10m, "Hack")));
        await Assert.ThrowsAsync<NotFoundException>(() => new DeleteIncomeHandler(userBGuard, _incomes, _unitOfWork).ExecuteAsync(new DeleteIncomeCommand(income.Id)));

        // Fixed incomes
        await Assert.ThrowsAsync<NotFoundException>(() => new CreateFixedIncomeHandler(userBGuard, _fixedIncomes, _conditions, _unitOfWork).ExecuteAsync(new CreateFixedIncomeCommand(account.Id, 2026, 11, null, null, 5, 100m, "Hack")));
        await Assert.ThrowsAsync<NotFoundException>(() => new ListFixedIncomesHandler(userBGuard, _fixedIncomes, _conditions, _clock).ExecuteAsync(new ListFixedIncomesQuery(account.Id, 2026, 11)));
        await Assert.ThrowsAsync<NotFoundException>(() => new UpdateFixedIncomeHandler(userBGuard, _fixedIncomes, _conditions, _clock, _unitOfWork).ExecuteAsync(new UpdateFixedIncomeCommand(fixedIncome.Id, "Hack", 5)));
        await Assert.ThrowsAsync<NotFoundException>(() => new StopFixedIncomeHandler(userBGuard, _fixedIncomes, _unitOfWork).ExecuteAsync(new StopFixedIncomeCommand(fixedIncome.Id, 2026, 12)));
        await Assert.ThrowsAsync<NotFoundException>(() => new ConfirmFixedIncomeReceiptHandler(userBGuard, _fixedIncomes, _clock, _unitOfWork).ExecuteAsync(new ConfirmFixedIncomeReceiptCommand(fixedIncome.Id, 2026, 11, new DateOnly(2026, 11, 5), 100m)));
        await Assert.ThrowsAsync<NotFoundException>(() => new RevertFixedIncomeReceiptHandler(userBGuard, _fixedIncomes, _unitOfWork).ExecuteAsync(new RevertFixedIncomeReceiptCommand(fixedIncome.Id, 2026, 11)));
        await Assert.ThrowsAsync<NotFoundException>(() => new DeleteFixedIncomeHandler(userBGuard, _fixedIncomes, _unitOfWork).ExecuteAsync(new DeleteFixedIncomeCommand(fixedIncome.Id)));

        // Invoices (derived summary)
        await Assert.ThrowsAsync<NotFoundException>(() => new GetInvoiceSummaryHandler(userBGuard, _creditCards, _purchases, _clock).ExecuteAsync(new GetInvoiceSummaryQuery(account.Id, 2026, 11)));

        // Reports & Activities
        await Assert.ThrowsAsync<NotFoundException>(() => new GetMonthlyDashboardHandler(userBGuard, _creditCards, _categories, _budgets, _incomes, _fixedIncomes, _directExpenses, _fixedExpenses, _purchases, _clock).ExecuteAsync(new GetMonthlyDashboardQuery(account.Id, YearMonth.Of(2026, 11))));
        await Assert.ThrowsAsync<NotFoundException>(() => new ListActivitiesHandler(userBGuard, _categories, _incomes, _fixedIncomes, _directExpenses, _fixedExpenses, _purchases, _clock).ExecuteAsync(new ListActivitiesQuery(account.Id, 2026, 11)));

        // Nothing was mutated
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }
}
