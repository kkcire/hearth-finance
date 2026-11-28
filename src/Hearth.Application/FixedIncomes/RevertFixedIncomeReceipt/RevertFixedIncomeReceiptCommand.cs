namespace Hearth.Application.FixedIncomes.RevertFixedIncomeReceipt;

public sealed record RevertFixedIncomeReceiptCommand(
    Guid FixedIncomeId,
    int Year,
    int Month);
