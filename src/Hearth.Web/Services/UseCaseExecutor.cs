using Hearth.Application.Exceptions;
using Hearth.Domain.Exceptions;
using MudBlazor;

namespace Hearth.Web.Services;

/// <summary>
/// Executes Application handlers inside short-lived DI scopes so DbContext instances
/// are never held for the entire lifetime of a Blazor Server circuit.
/// </summary>
public sealed class UseCaseExecutor
{
    public const string UnexpectedErrorMessage =
        "Ocorreu um erro inesperado ao processar a operação. Tente novamente.";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISnackbar _snackbar;
    private readonly ILogger<UseCaseExecutor> _logger;

    public UseCaseExecutor(
        IServiceScopeFactory scopeFactory,
        ISnackbar snackbar,
        ILogger<UseCaseExecutor> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(snackbar);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
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
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Domain rule violation in {Handler}.", typeof(THandler).Name);
            ShowError(ex.Message, Severity.Warning, showErrorSnackbar);
            return ExecutionResult<TResult>.BusinessFailure(ex.Message);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Resource not found in {Handler}.", typeof(THandler).Name);
            ShowError(ex.Message, Severity.Warning, showErrorSnackbar);
            return ExecutionResult<TResult>.BusinessFailure(ex.Message);
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
            ShowError(ex.Message, Severity.Warning, showErrorSnackbar);
            return ExecutionResult<TResult>.BusinessFailure(ex.Message);
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

    private void ShowError(string message, Severity severity, bool showSnackbar)
    {
        if (showSnackbar)
        {
            _snackbar.Add(message, severity);
        }
    }
}
