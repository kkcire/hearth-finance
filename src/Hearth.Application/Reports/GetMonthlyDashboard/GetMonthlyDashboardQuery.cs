using Hearth.Domain.Services;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Reports.GetMonthlyDashboard;

public sealed record GetMonthlyDashboardQuery(
    Guid AccountId,
    YearMonth Month,
    Guid? ConditionId = null,
    DateOnly? BalanceAsOfDate = null,
    int HistoryMonths = MonthlyDashboardCalculator.DefaultHistoryMonths);
