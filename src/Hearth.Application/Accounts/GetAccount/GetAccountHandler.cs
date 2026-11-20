using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.Accounts.GetAccount;

public sealed class GetAccountHandler
{
    private readonly IAccountRepository _accounts;

    public GetAccountHandler(IAccountRepository accounts)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        _accounts = accounts;
    }

    public async Task<AccountDto> ExecuteAsync(
        GetAccountQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var account = await _accounts.GetByIdAsync(query.AccountId, cancellationToken)
                      ?? throw new NotFoundException(nameof(Account), query.AccountId);

        return AccountDto.FromEntity(account);
    }
}
