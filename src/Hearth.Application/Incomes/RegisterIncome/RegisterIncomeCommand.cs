namespace Hearth.Application.Incomes.RegisterIncome;

public sealed record RegisterIncomeCommand(
    Guid AccountId,
    DateOnly ExpectedDate,
    decimal Amount,
    string Description,
    DateOnly? ReceivedOn = null);
