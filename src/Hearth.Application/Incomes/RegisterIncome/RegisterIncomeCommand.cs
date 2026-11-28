namespace Hearth.Application.Incomes.RegisterIncome;

public sealed record RegisterIncomeCommand(
    Guid AccountId,
    DateOnly Date,
    decimal Amount,
    string Description,
    Guid? ConditionId = null);
