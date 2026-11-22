using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;

namespace Hearth.Application.Accounts.RenameAccount;

public sealed class RenameAccountHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IAccountRepository _accounts;
    private readonly IUnitOfWork _unitOfWork;

    public RenameAccountHandler(
        AccountAccessGuard accountAccess,
        IAccountRepository accounts,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _accounts = accounts;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        RenameAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var account = await _accountAccess.GetAuthorizedAccountAsync(command.AccountId, cancellationToken);

        account.Rename(command.Name);

        await _accounts.UpdateAsync(account, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
