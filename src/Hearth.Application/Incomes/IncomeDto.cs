using Hearth.Domain.Entities;

namespace Hearth.Application.Incomes;

public sealed record IncomeDto(
    Guid Id,
    Guid AccountId,
    DateOnly ExpectedDate,
    decimal Amount,
    string Description,
    DateOnly? ReceivedOn,
    bool IsReceived)
{
    internal static IncomeDto FromEntity(Income income) =>
        new(
            income.Id,
            income.AccountId,
            income.ExpectedDate,
            income.Amount.Amount,
            income.Description,
            income.ReceivedOn,
            income.IsReceived);
}
