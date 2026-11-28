namespace Hearth.Application.FixedIncomes.CreateFixedIncome;

public sealed record CreateFixedIncomeCommand(
    Guid AccountId,
    int StartYear,
    int StartMonth,
    int? EndYear,
    int? EndMonth,
    int ExpectedDayOfMonth,
    decimal Amount,
    string Description,
    Guid? ConditionId = null);
