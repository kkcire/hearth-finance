using Hearth.Application.Abstractions;

namespace Hearth.Application.Accounts.ListAccounts;

public sealed class ListAccountsHandler
{
    private readonly IAccountRepository _accounts;

    public ListAccountsHandler(IAccountRepository accounts)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        _accounts = accounts;
    }

    public async Task<IReadOnlyList<AccountDto>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var accounts = await _accounts.GetAllAsync(cancellationToken);

        return accounts.Select(AccountDto.FromEntity).ToList();
    }
}
