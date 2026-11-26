using Hearth.Application.PurchaseConditions;
using Hearth.Application.Reports.GetMonthlyDashboard;
using Hearth.Domain.ValueObjects;

namespace Hearth.Web.Models;

/// <summary>
/// Read-only snapshot of the consolidated Monthly Dashboard (/) state.
/// </summary>
public sealed record DashboardViewModel(
    YearMonth SelectedMonth,
    DateOnly RealBalanceUntil,
    Guid? SelectedConditionId,
    IReadOnlyList<PurchaseConditionDto> Conditions,
    MonthlyDashboardDto? Dashboard);
