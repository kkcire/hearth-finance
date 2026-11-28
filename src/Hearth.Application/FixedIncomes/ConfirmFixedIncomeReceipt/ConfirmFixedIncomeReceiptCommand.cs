namespace Hearth.Application.FixedIncomes.ConfirmFixedIncomeReceipt;

public sealed record ConfirmFixedIncomeReceiptCommand(
    Guid FixedIncomeId,
    int Year,
    int Month,
    DateOnly ReceivedOn,
    decimal ActualAmount);
