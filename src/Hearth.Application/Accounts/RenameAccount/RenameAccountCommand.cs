namespace Hearth.Application.Accounts.RenameAccount;

public sealed record RenameAccountCommand(Guid AccountId, string Name);
