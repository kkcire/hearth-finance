namespace Hearth.Application.Incomes.MarkIncomeAsReceived;

public sealed record MarkIncomeAsReceivedCommand(Guid IncomeId, DateOnly ReceivedOn);
