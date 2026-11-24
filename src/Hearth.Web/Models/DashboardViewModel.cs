using Hearth.Application.CreditCards;
using Hearth.Application.Invoices.GetInvoiceSummary;
using Hearth.Application.PurchaseConditions;
using Hearth.Application.Reports.GetBudgetStatus;
using Hearth.Application.Reports.GetExpensesByCategory;
using Hearth.Application.Reports.GetMonthlySurplus;
using Hearth.Application.Reports.GetProjectedResult;
using Hearth.Application.Reports.GetRealBalance;
using Hearth.Domain.ValueObjects;

namespace Hearth.Web.Models;

/// <summary>
/// Read-only snapshot of the Dashboard (/) functional state, ready to feed cards,
/// cascade breakdowns, and evolution charts without coupling UI layout to query handlers.
/// </summary>
public sealed record DashboardViewModel(
    YearMonth SelectedMonth,
    DateOnly RealBalanceUntil,
    Guid? SelectedConditionId,
    IReadOnlyList<PurchaseConditionDto> Conditions,
    RealBalanceDto? RealBalance,
    MonthlySurplusDto? MonthlySurplus,
    InvoiceSummaryDto? InvoiceSummary,
    ProjectedResultDto? ProjectedResult,
    CreditCardDto? ActiveCard,
    BudgetStatusDto? BudgetStatus,
    CategorySpendingDto? CategorySpending);
