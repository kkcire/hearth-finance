namespace Hearth.Application.FixedExpenses.ChangeFixedExpenseAmount;

public sealed record ChangeFixedExpenseAmountCommand(Guid FixedExpenseId, decimal NewAmount);

public sealed record ChangeFixedExpenseAmountResult(int EffectiveFromYear, int EffectiveFromMonth);
