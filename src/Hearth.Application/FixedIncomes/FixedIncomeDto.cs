using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.FixedIncomes;

public sealed record FixedIncomeAmountChangeDto(
    int EffectiveFromYear,
    int EffectiveFromMonth,
    decimal Amount);

public sealed record FixedIncomeReceiptDto(
    int Year,
    int Month,
    DateOnly ReceivedOn,
    decimal ActualAmount);

public sealed record FixedIncomeDto(
    Guid Id,
    Guid AccountId,
    string Description,
    Guid ConditionId,
    string ConditionName,
    bool IsOwn,
    int ExpectedDayOfMonth,
    int StartYear,
    int StartMonth,
    int? EndYear,
    int? EndMonth,
    bool IsActiveForMonth,
    decimal? ExpectedAmount,
    bool IsReceivedForMonth,
    DateOnly? ReceivedOn,
    decimal? ActualAmount,
    IReadOnlyList<FixedIncomeAmountChangeDto> AmountHistory,
    IReadOnlyList<FixedIncomeReceiptDto> Receipts)
{
    internal static FixedIncomeDto FromEntity(
        FixedIncome income,
        YearMonth targetMonth,
        string conditionName)
    {
        var isActive = income.IsActiveIn(targetMonth);
        decimal? expectedAmount = isActive ? income.GetExpectedAmountFor(targetMonth).Amount : null;
        var receipt = income.GetReceiptFor(targetMonth);

        return new FixedIncomeDto(
            income.Id,
            income.AccountId,
            income.Description,
            income.ConditionId,
            conditionName,
            income.IsOwn,
            income.ExpectedDayOfMonth,
            income.StartMonth.Year,
            income.StartMonth.Month,
            income.EndMonth?.Year,
            income.EndMonth?.Month,
            isActive,
            expectedAmount,
            receipt is not null,
            receipt?.ReceivedOn,
            receipt?.ActualAmount.Amount,
            income.AmountHistory
                .Select(change => new FixedIncomeAmountChangeDto(
                    change.EffectiveFrom.Year,
                    change.EffectiveFrom.Month,
                    change.Amount.Amount))
                .ToList(),
            income.Receipts
                .Select(entry => new FixedIncomeReceiptDto(
                    entry.Month.Year,
                    entry.Month.Month,
                    entry.ReceivedOn,
                    entry.ActualAmount.Amount))
                .ToList());
    }
}
