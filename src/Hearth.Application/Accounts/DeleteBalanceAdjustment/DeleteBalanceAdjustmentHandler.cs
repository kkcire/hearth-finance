using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.Accounts.DeleteBalanceAdjustment;

public sealed class DeleteBalanceAdjustmentHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IAccountRepository _accounts;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteBalanceAdjustmentHandler(
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
        DeleteBalanceAdjustmentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var account = await _accountAccess.GetAuthorizedAccountWithBalanceHistoryAsync(command.AccountId, cancellationToken);
        var removed = account.RemoveBalanceAdjustment(command.AdjustmentId)
                      ?? throw new NotFoundException(nameof(BalanceAdjustment), command.AdjustmentId);

        await _accounts.RemoveBalanceAdjustmentAsync(removed, cancellationToken);
        await _accounts.UpdateAsync(account, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
