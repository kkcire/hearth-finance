using Hearth.Application.Abstractions;
using Hearth.Web.Services;
using Microsoft.AspNetCore.Components;

namespace Hearth.Web.Components;

/// <summary>
/// Base component for pages scoped to the active account.
/// Automatically subscribes to and unsubscribes from <see cref="AccountContext.Changed"/>
/// to prevent circuit memory leaks.
/// </summary>
public abstract class AccountPageBase : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    [Inject]
    protected AccountContext AccountContext { get; set; } = default!;

    [Inject]
    protected UseCaseExecutor Executor { get; set; } = default!;

    [Inject]
    protected IClock Clock { get; set; } = default!;

    protected bool HasActiveAccount => AccountContext.Current is not null;

    protected Guid AccountId => AccountContext.Current?.Id
        ?? throw new InvalidOperationException("No active account is selected.");

    protected CancellationToken ComponentCancellationToken => _cts.Token;

    protected override async Task OnInitializedAsync()
    {
        AccountContext.Changed += HandleAccountChanged;

        if (!AccountContext.IsInitialized)
        {
            await AccountContext.InitializeAsync(_cts.Token);
        }

        if (AccountContext.Current is not null)
        {
            await LoadDataAsync(_cts.Token);
        }
    }

    protected abstract Task LoadDataAsync(CancellationToken cancellationToken);

    private void HandleAccountChanged()
    {
        if (_disposed)
        {
            return;
        }

        _ = InvokeAsync(ReloadOnAccountChangeAsync);
    }

    private async Task ReloadOnAccountChangeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (AccountContext.Current is not null)
            {
                await LoadDataAsync(_cts.Token);
            }

            if (!_disposed)
            {
                StateHasChanged();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public virtual void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        AccountContext.Changed -= HandleAccountChanged;
        _cts.Cancel();
        _cts.Dispose();
    }
}
