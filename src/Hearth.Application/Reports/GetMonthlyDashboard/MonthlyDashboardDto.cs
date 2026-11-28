using Hearth.Application.Accounts;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Reports.GetMonthlyDashboard;

public sealed record MonthlyDashboardDto(
    Guid AccountId,
    YearMonth SelectedMonth,
    Guid? ConditionId,
    DashboardBalanceDto Balance,
    DashboardInvoiceDto CurrentInvoice,
    DashboardCurrentMonthFlowDto CurrentMonthFlow,
    IReadOnlyList<DashboardMonthlyResultPointDto> MonthlyResultHistory,
    IReadOnlyList<DashboardCategorySpendingItemDto> CategorySpending,
    IReadOnlyList<DashboardRecentActivityDto> RecentActivities,
    DashboardNextMonthEstimateDto? NextMonthEstimate);

public sealed record DashboardBalanceDto(
    decimal Amount,
    DateOnly AsOfDate,
    decimal InitialBalance,
    decimal ReceivedIncomes,
    decimal DirectExpenses,
    decimal PaidFixedExpenses,
    decimal PaidInvoices,
    IReadOnlyList<BalanceAdjustmentDto>? BalanceHistory = null);

public sealed record DashboardInvoiceDto(
    YearMonth BillingMonth,
    decimal CycleAmount,
    DateOnly? DueDate,
    DateOnly? ClosingDate,
    InvoiceStatus? Status,
    decimal FutureInstallmentsTotal,
    bool HasActiveCard,
    string? CardName);

public sealed record DashboardCurrentMonthFlowDto(
    decimal MonthlyIncome,
    decimal TotalExpenses,
    decimal DirectExpenses,
    decimal FixedExpenses,
    decimal CardInvoice,
    decimal MonthlyResult,
    decimal SavingsRatePercentage,
    BreakEvenStatus BreakEvenStatus,
    decimal CommittedFloor,
    IReadOnlyList<DashboardIncomeEntryDto>? IncomeEntries = null,
    decimal OwnMonthlyIncome = 0m);

public sealed record DashboardIncomeEntryDto(
    Guid Id,
    string Description,
    DateOnly Date,
    decimal Amount,
    bool IsReceived);

public sealed record DashboardMonthlyResultPointDto(
    YearMonth YearMonth,
    decimal Income,
    decimal Expenses,
    decimal Result,
    BreakEvenStatus BreakEvenStatus,
    bool IsProjection);

public sealed record DashboardCategorySpendingItemDto(
    Guid CategoryId,
    string CategoryName,
    string HexColor,
    decimal AmountSpent,
    decimal ShareOfTotalSpending,
    decimal? BudgetAmount,
    decimal? BudgetUsagePercentage,
    IReadOnlyList<DashboardCategoryExpenseEntryDto>? Entries = null);

public sealed record DashboardCategoryExpenseEntryDto(
    Guid Id,
    string Description,
    DateOnly Date,
    decimal Amount,
    string OriginLabel,
    string? InstallmentLabel = null);

public sealed record DashboardRecentActivityDto(
    Guid Id,
    string Description,
    DateOnly Date,
    decimal Amount,
    string MovementType,
    string CategoryIcon,
    string StatusLabel,
    string? InstallmentLabel = null);

public sealed record DashboardNextMonthEstimateDto(
    YearMonth NextYearMonth,
    decimal EstimatedIncome,
    decimal EstimatedFixedExpenses,
    decimal EstimatedCardInstallments,
    decimal EstimatedDirectExpenses,
    decimal EstimatedCommitments,
    decimal ProjectedResult,
    decimal EstimatedSavingsRatePercentage,
    decimal? ProjectedOpeningBalance = null,
    decimal? ProjectedEndingBalance = null);
