using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Accounts.ConfirmInitialBalance;

public sealed class ConfirmInitialBalanceHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IAccountRepository _accounts;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmInitialBalanceHandler(
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
        ConfirmInitialBalanceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var account = await _accountAccess.GetAuthorizedAccountAsync(command.AccountId, cancellationToken);

        if (command.Skip)
        {
            account.SkipInitialBalance();
        }
        else
        {
            var amount = Money.FromDecimal(command.InitialBalance);
            account.ConfirmInitialBalance(amount);
        }

        await _accounts.UpdateAsync(account, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
