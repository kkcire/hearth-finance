namespace Hearth.Application.Incomes.UpdateIncome;

public sealed record UpdateIncomeCommand(
    Guid IncomeId,
    DateOnly Date,
    decimal Amount,
    string Description,
    Guid? ConditionId = null);
