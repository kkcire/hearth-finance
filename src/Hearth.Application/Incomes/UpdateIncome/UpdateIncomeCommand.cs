namespace Hearth.Application.Incomes.UpdateIncome;

public sealed record UpdateIncomeCommand(
    Guid IncomeId,
    DateOnly ExpectedDate,
    decimal Amount,
    string Description,
    Guid? ConditionId = null);
