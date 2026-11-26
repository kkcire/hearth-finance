namespace Hearth.Domain.ValueObjects;

/// <summary>
/// Consolidated domain snapshot powering the Monthly Dashboard hub.
/// </summary>
public sealed record MonthlyDashboardSnapshot(
    RealBalance Balance,
    CurrentInvoiceSummary CurrentInvoice,
    MonthlyFlowSummary CurrentMonthFlow,
    IReadOnlyList<MonthlyFlowSummary> MonthlyResultHistory,
    NextMonthEstimate? NextMonthEstimate,
    Guid? ConditionId);
