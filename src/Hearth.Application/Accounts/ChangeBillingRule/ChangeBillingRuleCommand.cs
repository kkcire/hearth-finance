namespace Hearth.Application.Accounts.ChangeBillingRule;

public sealed record ChangeBillingRuleCommand(Guid AccountId, int ClosingDay, int DueDay);
