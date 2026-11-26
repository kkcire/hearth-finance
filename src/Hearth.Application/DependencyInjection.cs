using Hearth.Application.Accounts.ConfirmInitialBalance;
using Hearth.Application.Accounts.CreateAccount;
using Hearth.Application.Accounts.DeleteBalanceAdjustment;
using Hearth.Application.Accounts.GetAccount;
using Hearth.Application.Accounts.ListAccounts;
using Hearth.Application.Accounts.RenameAccount;
using Hearth.Application.Accounts.SetAccountBalance;
using Hearth.Application.Activities.ListActivities;
using Hearth.Application.Auth.AuthenticateUser;
using Hearth.Application.Auth.RegisterUser;
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
using Hearth.Application.PurchaseConditions.CreatePurchaseCondition;
using Hearth.Application.PurchaseConditions.ListPurchaseConditions;
using Hearth.Application.Reports.GetMonthlyDashboard;
using Microsoft.Extensions.DependencyInjection;

namespace Hearth.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Auth & Authorization
        services.AddScoped<AccountAccessGuard>();
        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<AuthenticateUserHandler>();

        // Accounts
        services.AddScoped<CreateAccountHandler>();
        services.AddScoped<ConfirmInitialBalanceHandler>();
        services.AddScoped<GetAccountHandler>();
        services.AddScoped<ListAccountsHandler>();
        services.AddScoped<RenameAccountHandler>();
        services.AddScoped<SetAccountBalanceHandler>();
        services.AddScoped<DeleteBalanceAdjustmentHandler>();

        // Categories
        services.AddScoped<CreateCategoryHandler>();
        services.AddScoped<RenameCategoryHandler>();
        services.AddScoped<ArchiveCategoryHandler>();
        services.AddScoped<RestoreCategoryHandler>();
        services.AddScoped<DeleteCategoryHandler>();
        services.AddScoped<ListCategoriesHandler>();

        // Category budgets
        services.AddScoped<CreateCategoryBudgetHandler>();
        services.AddScoped<ChangeCategoryBudgetAmountHandler>();
        services.AddScoped<EndCategoryBudgetHandler>();
        services.AddScoped<DeleteCategoryBudgetHandler>();
        services.AddScoped<ListCategoryBudgetsHandler>();

        // Direct expenses
        services.AddScoped<RegisterDirectExpenseHandler>();
        services.AddScoped<UpdateDirectExpenseHandler>();
        services.AddScoped<DeleteDirectExpenseHandler>();
        services.AddScoped<ListDirectExpensesHandler>();

        // Fixed expenses
        services.AddScoped<RegisterFixedExpenseHandler>();
        services.AddScoped<UpdateFixedExpenseDetailsHandler>();
        services.AddScoped<ChangeFixedExpenseAmountHandler>();
        services.AddScoped<EndFixedExpenseHandler>();
        services.AddScoped<MarkFixedExpenseAsPaidHandler>();
        services.AddScoped<RevertFixedExpensePaymentHandler>();
        services.AddScoped<DeleteFixedExpenseHandler>();
        services.AddScoped<ListFixedExpensesHandler>();

        // Credit cards
        services.AddScoped<CreateCreditCardHandler>();
        services.AddScoped<UpdateCreditCardHandler>();
        services.AddScoped<ArchiveCreditCardHandler>();
        services.AddScoped<RestoreCreditCardHandler>();
        services.AddScoped<DeleteCreditCardHandler>();
        services.AddScoped<ListCreditCardsHandler>();

        // Credit card purchases
        services.AddScoped<RegisterCreditCardPurchaseHandler>();
        services.AddScoped<UpdateCreditCardPurchaseAmountHandler>();
        services.AddScoped<UpdateCreditCardPurchaseDateHandler>();
        services.AddScoped<UpdateCreditCardPurchaseDetailsHandler>();
        services.AddScoped<DeleteCreditCardPurchaseHandler>();
        services.AddScoped<ListCreditCardPurchasesHandler>();

        // Purchase conditions
        services.AddScoped<CreatePurchaseConditionHandler>();
        services.AddScoped<ListPurchaseConditionsHandler>();

        // Incomes
        services.AddScoped<RegisterIncomeHandler>();
        services.AddScoped<UpdateIncomeHandler>();
        services.AddScoped<MarkIncomeAsReceivedHandler>();
        services.AddScoped<RevertIncomeToExpectedHandler>();
        services.AddScoped<DeleteIncomeHandler>();
        services.AddScoped<ListIncomesHandler>();

        // Invoices (derived)
        services.AddScoped<GetInvoiceSummaryHandler>();

        // Reports
        services.AddScoped<GetMonthlyDashboardHandler>();

        // Activities
        services.AddScoped<ListActivitiesHandler>();

        return services;
    }
}
