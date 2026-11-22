using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Accounts.UpdateInitialBalance;

public sealed class UpdateInitialBalanceHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IAccountRepository _accounts;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateInitialBalanceHandler(
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
        UpdateInitialBalanceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var newBalance = Money.FromDecimal(command.InitialBalance);

        var account = await _accountAccess.GetAuthorizedAccountAsync(command.AccountId, cancellationToken);

        account.UpdateInitialBalance(newBalance);

        await _accounts.UpdateAsync(account, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
