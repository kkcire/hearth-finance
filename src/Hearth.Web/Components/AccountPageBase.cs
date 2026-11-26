using Hearth.Application.Abstractions;
using Hearth.Domain.ValueObjects;
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
    private bool _suppressAccountChangeReload;

    [Inject]
    protected AccountContext AccountContext { get; set; } = default!;

    [Inject]
    protected UseCaseExecutor Executor { get; set; } = default!;

    [Inject]
    protected IClock Clock { get; set; } = default!;

    protected bool HasActiveAccount => AccountContext.Current is not null;

    protected Guid AccountId => AccountContext.Current?.Id
        ?? throw new InvalidOperationException("No active account is selected.");

    protected DateOnly? TrackingStartDate => AccountContext.Current?.TrackingStartDate;

    protected YearMonth? TrackingStartPeriod => AccountContext.Current?.TrackingStartPeriod;

    protected CancellationToken ComponentCancellationToken => _cts.Token;

    protected override async Task OnInitializedAsync()
    {
        if (!AccountContext.IsInitialized)
        {
            await AccountContext.InitializeAsync(_cts.Token);
        }

        if (_disposed)
        {
            return;
        }

        AccountContext.Changed += HandleAccountChanged;

        if (AccountContext.Current is not null)
        {
            await LoadDataAsync(_cts.Token);
        }
    }

    protected abstract Task LoadDataAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Invoked whenever the active account changes in <see cref="AccountContext"/>, before
    /// <see cref="LoadDataAsync"/> runs. Override to close open forms/dialogs and reset stale state.
    /// </summary>
    protected virtual void OnAccountChanged()
    {
    }

    /// <summary>
    /// Reloads the shared <see cref="AccountContext"/> without triggering a redundant
    /// <see cref="LoadDataAsync"/> pass on this page (caller refreshes its own view directly).
    /// </summary>
    protected async Task ReloadAccountContextAsync(Guid? selectAccountId = null, CancellationToken cancellationToken = default)
    {
        _suppressAccountChangeReload = true;
        try
        {
            await AccountContext.ReloadAsync(selectAccountId, cancellationToken);
        }
        finally
        {
            _suppressAccountChangeReload = false;
        }
    }

    private void HandleAccountChanged()
    {
        if (_disposed || _suppressAccountChangeReload)
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
            OnAccountChanged();

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
