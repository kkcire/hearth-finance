using Hearth.Application.Accounts.ChangeBillingRule;
using Hearth.Application.Accounts.CreateAccount;
using Hearth.Application.Accounts.GetAccount;
using Hearth.Application.Accounts.ListAccounts;
using Hearth.Application.Accounts.RenameAccount;
using Hearth.Application.Accounts.UpdateInitialBalance;
using Hearth.Application.Auth.AuthenticateUser;
using Hearth.Application.Auth.RegisterUser;
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
using Microsoft.Extensions.DependencyInjection;

namespace Hearth.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Auth
        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<AuthenticateUserHandler>();

        // Accounts
        services.AddScoped<CreateAccountHandler>();
        services.AddScoped<GetAccountHandler>();
        services.AddScoped<ListAccountsHandler>();
        services.AddScoped<RenameAccountHandler>();
        services.AddScoped<UpdateInitialBalanceHandler>();
        services.AddScoped<ChangeBillingRuleHandler>();

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

        // Credit card purchases
        services.AddScoped<RegisterCreditCardPurchaseHandler>();
        services.AddScoped<UpdateCreditCardPurchaseAmountHandler>();
        services.AddScoped<UpdateCreditCardPurchaseDateHandler>();
        services.AddScoped<UpdateCreditCardPurchaseDetailsHandler>();
        services.AddScoped<DeleteCreditCardPurchaseHandler>();
        services.AddScoped<ListCreditCardPurchasesHandler>();

        // Incomes
        services.AddScoped<RegisterIncomeHandler>();
        services.AddScoped<UpdateIncomeHandler>();
        services.AddScoped<MarkIncomeAsReceivedHandler>();
        services.AddScoped<RevertIncomeToExpectedHandler>();
        services.AddScoped<DeleteIncomeHandler>();
        services.AddScoped<ListIncomesHandler>();

        // Invoices
        services.AddScoped<InformInvoiceChargedAmountHandler>();
        services.AddScoped<MarkInvoiceAsPaidHandler>();
        services.AddScoped<RevertInvoicePaymentHandler>();
        services.AddScoped<ListInvoicesHandler>();
        services.AddScoped<GetInvoiceSummaryHandler>();

        // Reports
        services.AddScoped<GetProjectedResultHandler>();
        services.AddScoped<GetRealBalanceHandler>();
        services.AddScoped<GetExpensesByCategoryHandler>();
        services.AddScoped<GetBudgetStatusHandler>();

        return services;
    }
}
