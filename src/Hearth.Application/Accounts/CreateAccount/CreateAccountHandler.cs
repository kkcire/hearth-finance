using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Accounts.CreateAccount;

public sealed class CreateAccountHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IAccountRepository _accounts;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAccountHandler(
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
        CreateAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var ownerUserId = _accountAccess.GetRequiredUserId();
        var initialBalance = Money.FromDecimal(command.InitialBalance);

        var account = Account.Create(
            Guid.CreateVersion7(),
            ownerUserId,
            command.Name,
            initialBalance,
            _clock.Today,
            command.IsInitialBalanceConfirmed);

        await _accounts.AddAsync(account, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return account.Id;
    }
}
