using Hearth.Application.Exceptions;
using Hearth.Domain.Exceptions;
using Hearth.Web.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;

namespace Hearth.Web.Services;

/// <summary>
/// Executes Application handlers inside short-lived DI scopes so DbContext instances
/// are never held for the entire lifetime of a Blazor Server circuit, while propagating
/// the authenticated circuit user into each child scope's <see cref="ScopedCurrentUser"/>.
/// </summary>
public sealed class UseCaseExecutor
{
    public const string UnexpectedErrorMessage =
        "Ocorreu um erro inesperado ao processar a operação. Tente novamente.";

    public const string SessionExpiredMessage =
        "Sua sessão expirou. Faça login novamente.";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly ISnackbar _snackbar;
    private readonly ILogger<UseCaseExecutor> _logger;

    public UseCaseExecutor(
        IServiceScopeFactory scopeFactory,
        AuthenticationStateProvider authenticationStateProvider,
        ISnackbar snackbar,
        ILogger<UseCaseExecutor> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(authenticationStateProvider);
        ArgumentNullException.ThrowIfNull(snackbar);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
        _authenticationStateProvider = authenticationStateProvider;
        _snackbar = snackbar;
        _logger = logger;
    }

    public async Task<ExecutionResult<TResult>> ExecuteAsync<THandler, TResult>(
        Func<THandler, CancellationToken, Task<TResult>> operation,
        string? successMessage = null,
        bool showErrorSnackbar = true,
        CancellationToken cancellationToken = default)
        where THandler : notnull
    {
        ArgumentNullException.ThrowIfNull(operation);

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await PopulateCurrentUserAsync(scope.ServiceProvider);

            var handler = scope.ServiceProvider.GetRequiredService<THandler>();
            var result = await operation(handler, cancellationToken);

            if (!string.IsNullOrWhiteSpace(successMessage))
            {
                _snackbar.Add(successMessage, Severity.Success);
            }

            return ExecutionResult<TResult>.Success(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthenticated execution attempt in {Handler}.", typeof(THandler).Name);
            ShowError(SessionExpiredMessage, Severity.Warning, showErrorSnackbar);
            return ExecutionResult<TResult>.BusinessFailure(SessionExpiredMessage);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Domain rule violation in {Handler}.", typeof(THandler).Name);
            var message = TranslateBusinessMessage(ex.Message);
            ShowError(message, Severity.Warning, showErrorSnackbar);
            return ExecutionResult<TResult>.BusinessFailure(message);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Resource not found in {Handler}.", typeof(THandler).Name);
            var message = TranslateBusinessMessage(ex.Message);
            ShowError(message, Severity.Warning, showErrorSnackbar);
            return ExecutionResult<TResult>.BusinessFailure(message);
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogError(ex, "Unexpected null argument in {Handler}.", typeof(THandler).Name);
            ShowError(UnexpectedErrorMessage, Severity.Error, showErrorSnackbar);
            return ExecutionResult<TResult>.UnexpectedFailure(UnexpectedErrorMessage);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error in {Handler}.", typeof(THandler).Name);
            var message = TranslateBusinessMessage(ex.Message);
            ShowError(message, Severity.Warning, showErrorSnackbar);
            return ExecutionResult<TResult>.BusinessFailure(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while executing {Handler}.", typeof(THandler).Name);
            ShowError(UnexpectedErrorMessage, Severity.Error, showErrorSnackbar);
            return ExecutionResult<TResult>.UnexpectedFailure(UnexpectedErrorMessage);
        }
    }

    public async Task<ExecutionResult> ExecuteAsync<THandler>(
        Func<THandler, CancellationToken, Task> operation,
        string? successMessage = null,
        bool showErrorSnackbar = true,
        CancellationToken cancellationToken = default)
        where THandler : notnull
    {
        ArgumentNullException.ThrowIfNull(operation);

        var result = await ExecuteAsync<THandler, bool>(
            async (handler, ct) =>
            {
                await operation(handler, ct);
                return true;
            },
            successMessage,
            showErrorSnackbar,
            cancellationToken);

        return result.IsSuccess
            ? ExecutionResult.Success()
            : result.IsBusinessError
                ? ExecutionResult.BusinessFailure(result.ErrorMessage!)
                : ExecutionResult.UnexpectedFailure(result.ErrorMessage!);
    }

    private async Task PopulateCurrentUserAsync(IServiceProvider scopedServices)
    {
        var currentUser = scopedServices.GetService<ScopedCurrentUser>();
        if (currentUser is null)
        {
            return;
        }

        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        if (HearthAuth.TryReadUser(authState.User, out var userId, out var name, out var email))
        {
            currentUser.SetAuthenticated(userId, name, email);
        }
        else
        {
            currentUser.Clear();
        }
    }

    private static string TranslateBusinessMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return UnexpectedErrorMessage;
        }

        if (message.StartsWith("No entry can be registered before the tracking start date", StringComparison.Ordinal))
        {
            return "A data informada não pode ser anterior ao início do acompanhamento da conta.";
        }

        if (message.StartsWith("No entry can be registered before the tracking start period", StringComparison.Ordinal))
        {
            return "O período informado não pode ser anterior ao início do acompanhamento da conta.";
        }

        if (message.StartsWith("The account balance cannot be defined on a future date", StringComparison.Ordinal))
        {
            return "Não é possível definir o saldo em uma data futura.";
        }

        if (message.StartsWith("The balance definition reason cannot exceed", StringComparison.Ordinal))
        {
            return "O motivo não pode ultrapassar 200 caracteres.";
        }

        if (message.StartsWith("Monetary amounts support at most 2 decimal places", StringComparison.Ordinal))
        {
            return "Informe um valor monetário com no máximo 2 casas decimais.";
        }

        return message;
    }

    private void ShowError(string message, Severity severity, bool showSnackbar)
    {
        if (showSnackbar)
        {
            _snackbar.Add(message, severity);
        }
    }
}
