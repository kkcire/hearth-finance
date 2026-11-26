using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Accounts.SetAccountBalance;

public sealed class SetAccountBalanceHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IAccountRepository _accounts;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public SetAccountBalanceHandler(
        AccountAccessGuard accountAccess,
        IAccountRepository accounts,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _accounts = accounts;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> ExecuteAsync(
        SetAccountBalanceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var definedBalance = Money.FromDecimal(command.Balance);
        var account = await _accountAccess.GetAuthorizedAccountWithBalanceHistoryAsync(command.AccountId, cancellationToken);

        var entry = account.SetBalance(
            Guid.CreateVersion7(),
            definedBalance,
            command.Reason,
            _clock.UtcNow);

        await _accounts.UpdateAsync(account, cancellationToken);
        await _accounts.AddBalanceAdjustmentAsync(entry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entry.Id;
    }
}
