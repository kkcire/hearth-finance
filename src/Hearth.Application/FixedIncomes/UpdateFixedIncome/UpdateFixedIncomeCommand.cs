namespace Hearth.Application.FixedIncomes.UpdateFixedIncome;

public sealed record UpdateFixedIncomeCommand(
    Guid FixedIncomeId,
    string Description,
    int ExpectedDayOfMonth,
    Guid? ConditionId = null,
    decimal? NewAmount = null,
    int? EffectiveFromYear = null,
    int? EffectiveFromMonth = null);
