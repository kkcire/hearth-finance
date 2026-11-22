using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;

namespace Hearth.Application.Accounts.ListAccounts;

public sealed class ListAccountsHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IAccountRepository _accounts;

    public ListAccountsHandler(AccountAccessGuard accountAccess, IAccountRepository accounts)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(accounts);

        _accountAccess = accountAccess;
        _accounts = accounts;
    }

    public async Task<IReadOnlyList<AccountDto>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var ownerUserId = _accountAccess.GetRequiredUserId();
        var accounts = await _accounts.GetByOwnerAsync(ownerUserId, cancellationToken);

        return accounts.Select(AccountDto.FromEntity).ToList();
    }
}
