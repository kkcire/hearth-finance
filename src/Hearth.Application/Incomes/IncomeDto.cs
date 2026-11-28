using Hearth.Domain.Entities;

namespace Hearth.Application.Incomes;

public sealed record IncomeDto(
    Guid Id,
    Guid AccountId,
    DateOnly Date,
    decimal Amount,
    string Description,
    bool IsReceived,
    Guid ConditionId)
{
    internal static IncomeDto FromEntity(Income income, DateOnly today) =>
        new(
            income.Id,
            income.AccountId,
            income.Date,
            income.Amount.Amount,
            income.Description,
            income.IsReceivedOn(today),
            income.ConditionId);
}
