namespace Hearth.Application.FixedIncomes.StopFixedIncome;

public sealed record StopFixedIncomeCommand(
    Guid FixedIncomeId,
    int? EndYear,
    int? EndMonth);
