using Hearth.Application.Authorization;

namespace Hearth.Application.Accounts.GetAccount;

public sealed class GetAccountHandler
{
    private readonly AccountAccessGuard _accountAccess;

    public GetAccountHandler(AccountAccessGuard accountAccess)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);

        _accountAccess = accountAccess;
    }

    public async Task<AccountDto> ExecuteAsync(
        GetAccountQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var account = await _accountAccess.GetAuthorizedAccountWithBalanceHistoryAsync(query.AccountId, cancellationToken);

        return AccountDto.FromEntity(account);
    }
}
