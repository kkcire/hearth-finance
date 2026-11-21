using Hearth.Application.Accounts;
using Hearth.Application.Accounts.ListAccounts;

namespace Hearth.Web.Services;

/// <summary>
/// Holds the active account selection for the current Blazor Server circuit.
/// </summary>
public sealed class AccountContext
{
    private readonly UseCaseExecutor _executor;
    private Task<bool>? _inFlightInitialization;

    public AccountContext(UseCaseExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(executor);

        _executor = executor;
    }

    public bool IsInitialized { get; private set; }

    public IReadOnlyList<AccountDto> Accounts { get; private set; } = [];

    public AccountDto? Current { get; private set; }

    public Guid? CurrentAccountId => Current?.Id;

    public bool HasAccounts => Accounts.Count > 0;

    public string? LoadError { get; private set; }

    public event Action? Changed;

    public Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (IsInitialized)
        {
            return Task.FromResult(true);
        }

        _inFlightInitialization ??= RunInitializationAsync(cancellationToken);

        return _inFlightInitialization;
    }

    public async Task<bool> ReloadAsync(Guid? selectAccountId = null, CancellationToken cancellationToken = default)
    {
        var result = await _executor.ExecuteAsync<ListAccountsHandler, IReadOnlyList<AccountDto>>(
            (handler, ct) => handler.ExecuteAsync(ct),
            showErrorSnackbar: true,
            cancellationToken: cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            LoadError = result.ErrorMessage;
            Changed?.Invoke();
            return false;
        }

        LoadError = null;
        Accounts = result.Value;

        var preferredId = selectAccountId ?? Current?.Id;
        Current = preferredId.HasValue
            ? Accounts.FirstOrDefault(account => account.Id == preferredId.Value) ?? Accounts.FirstOrDefault()
            : Accounts.FirstOrDefault();

        IsInitialized = true;
        Changed?.Invoke();
        return true;
    }

    public void SelectAccount(Guid accountId)
    {
        var next = Accounts.FirstOrDefault(account => account.Id == accountId);
        if (next is null || Current?.Id == next.Id)
        {
            return;
        }

        Current = next;
        Changed?.Invoke();
    }

    private async Task<bool> RunInitializationAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await ReloadAsync(selectAccountId: null, cancellationToken);
        }
        finally
        {
            _inFlightInitialization = null;
        }
    }
}
