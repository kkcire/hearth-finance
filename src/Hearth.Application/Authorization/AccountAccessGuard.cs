using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.Authorization;

/// <summary>
/// Centralizes account ownership verification across all Application handlers.
/// Throws <see cref="UnauthorizedAccessException"/> when no user is authenticated and
/// <see cref="NotFoundException"/> when the target account does not exist or belongs to
/// another user, avoiding leaking resource existence across tenants.
/// </summary>
public sealed class AccountAccessGuard
{
    private readonly IAccountRepository _accounts;
    private readonly ICurrentUser _currentUser;

    public AccountAccessGuard(IAccountRepository accounts, ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(currentUser);

        _accounts = accounts;
        _currentUser = currentUser;
    }

    public Guid GetRequiredUserId() => _currentUser.GetRequiredUserId();

    public async Task<Account> GetAuthorizedAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var account = await _accounts.GetByIdAsync(accountId, cancellationToken);
        if (account is null || account.OwnerUserId != userId)
        {
            throw new NotFoundException(nameof(Account), accountId);
        }

        return account;
    }

    public async Task<Account> GetAuthorizedAccountWithBalanceHistoryAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var account = await _accounts.GetByIdWithBalanceHistoryAsync(accountId, cancellationToken);
        if (account is null || account.OwnerUserId != userId)
        {
            throw new NotFoundException(nameof(Account), accountId);
        }

        return account;
    }

    public async Task<Account> EnsureCanAccessEntityAsync(
        Guid accountId,
        string entityName,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var account = await _accounts.GetByIdAsync(accountId, cancellationToken);
        if (account is null || account.OwnerUserId != userId)
        {
            throw new NotFoundException(entityName, entityId);
        }

        return account;
    }
}
