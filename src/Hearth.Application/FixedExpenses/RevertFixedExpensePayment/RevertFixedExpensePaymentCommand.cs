namespace Hearth.Application.FixedExpenses.RevertFixedExpensePayment;

public sealed record RevertFixedExpensePaymentCommand(
    Guid FixedExpenseId,
    int Year,
    int Month);
