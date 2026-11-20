using Hearth.Domain.Entities;

namespace Hearth.Application.FixedExpenses;

public sealed record FixedExpenseAmountChangeDto(
    int EffectiveFromYear,
    int EffectiveFromMonth,
    decimal Amount);

public sealed record FixedExpensePaymentDto(
    int Year,
    int Month,
    DateOnly PaidOn);

public sealed record FixedExpenseDto(
    Guid Id,
    Guid AccountId,
    int StartYear,
    int StartMonth,
    int? EndYear,
    int? EndMonth,
    string Description,
    Guid CategoryId,
    IReadOnlyList<FixedExpenseAmountChangeDto> AmountHistory,
    IReadOnlyList<FixedExpensePaymentDto> Payments)
{
    internal static FixedExpenseDto FromEntity(FixedExpense expense) =>
        new(
            expense.Id,
            expense.AccountId,
            expense.StartMonth.Year,
            expense.StartMonth.Month,
            expense.EndMonth?.Year,
            expense.EndMonth?.Month,
            expense.Description,
            expense.CategoryId,
            expense.AmountHistory
                .Select(change => new FixedExpenseAmountChangeDto(
                    change.EffectiveFrom.Year,
                    change.EffectiveFrom.Month,
                    change.Amount.Amount))
                .ToList(),
            expense.Payments
                .OrderBy(payment => payment.Key)
                .Select(payment => new FixedExpensePaymentDto(
                    payment.Key.Year,
                    payment.Key.Month,
                    payment.Value))
                .ToList());
}
