using Hearth.Application.Accounts;
using Hearth.Application.Accounts.CreateAccount;
using Hearth.Application.Accounts.GetAccount;
using Hearth.Application.Accounts.ListAccounts;
using Hearth.Domain.Exceptions;
using Hearth.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Hearth.Tests.Web.Services;

public sealed class UseCaseExecutorTests : WebTestBase
{
    [Fact]
    public async Task ExecuteAsync_CreatesFreshScopePerCallAndPersistsAcrossSingletonFakes()
    {
        Services.AddScoped<ScopeProbeHandler>();

        var firstScopeId = await Executor.ExecuteAsync<ScopeProbeHandler, Guid>(
            (handler, _) => Task.FromResult(handler.InstanceId),
            showErrorSnackbar: false);

        var secondScopeId = await Executor.ExecuteAsync<ScopeProbeHandler, Guid>(
            (handler, _) => Task.FromResult(handler.InstanceId),
            showErrorSnackbar: false);

        Assert.True(firstScopeId.IsSuccess);
        Assert.True(secondScopeId.IsSuccess);
        Assert.NotEqual(firstScopeId.Value, secondScopeId.Value);

        var createdId = await Executor.ExecuteAsync<CreateAccountHandler, Guid>(
            (handler, ct) => handler.ExecuteAsync(
                new CreateAccountCommand("Pessoal", 500m, new DateOnly(2026, 1, 1)), ct),
            successMessage: "Conta criada.");

        var listed = await Executor.ExecuteAsync<ListAccountsHandler, IReadOnlyList<AccountDto>>(
            (handler, ct) => handler.ExecuteAsync(ct));

        Assert.True(createdId.IsSuccess);
        Assert.True(listed.IsSuccess);
        Assert.Single(listed.Value!, account => account.Id == createdId.Value);
        Assert.Contains(Snackbar.ShownSnackbars, item =>
            item.Severity == Severity.Success && item.Message == "Conta criada.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotFoundExceptionThrown_ReturnsBusinessFailureAndShowsWarning()
    {
        var result = await Executor.ExecuteAsync<GetAccountHandler, AccountDto>(
            (handler, ct) => handler.ExecuteAsync(new GetAccountQuery(Guid.CreateVersion7()), ct));

        Assert.False(result.IsSuccess);
        Assert.True(result.IsBusinessError);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains(Snackbar.ShownSnackbars, item => item.Severity == Severity.Warning);
    }

    [Fact]
    public async Task ExecuteAsync_WhenArgumentExceptionThrown_ReturnsBusinessFailure()
    {
        var result = await Executor.ExecuteAsync<CreateAccountHandler, Guid>(
            (handler, ct) => handler.ExecuteAsync(
                new CreateAccountCommand("Conta", 10.123m, new DateOnly(2026, 1, 1)), ct));

        Assert.False(result.IsSuccess);
        Assert.True(result.IsBusinessError);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains(Snackbar.ShownSnackbars, item => item.Severity == Severity.Warning);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDomainExceptionThrown_ReturnsBusinessFailure()
    {
        Services.AddScoped<ThrowingHandler>();

        var result = await Executor.ExecuteAsync<ThrowingHandler>(
            (handler, _) => handler.ThrowDomainAsync());

        Assert.False(result.IsSuccess);
        Assert.True(result.IsBusinessError);
        Assert.Equal("Business rule violated.", result.ErrorMessage);
        Assert.Contains(Snackbar.ShownSnackbars, item =>
            item.Severity == Severity.Warning && item.Message == "Business rule violated.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnexpectedExceptionThrown_ReturnsGenericFailureWithoutCrashing()
    {
        Services.AddScoped<ThrowingHandler>();

        var result = await Executor.ExecuteAsync<ThrowingHandler>(
            (handler, _) => handler.ThrowUnexpectedAsync());

        Assert.False(result.IsSuccess);
        Assert.False(result.IsBusinessError);
        Assert.Equal(UseCaseExecutor.UnexpectedErrorMessage, result.ErrorMessage);
        Assert.Contains(Snackbar.ShownSnackbars, item =>
            item.Severity == Severity.Error && item.Message == UseCaseExecutor.UnexpectedErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesAuthenticatedUserIntoFreshChildScope()
    {
        Services.AddScoped<CurrentUserProbeHandler>();

        var result = await Executor.ExecuteAsync<CurrentUserProbeHandler, Guid?>(
            (handler, _) => Task.FromResult(handler.CurrentUserId),
            showErrorSnackbar: false);

        Assert.True(result.IsSuccess);
        Assert.Equal(CurrentUser.UserId, result.Value);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnauthenticated_ReturnsSessionExpiredFailure()
    {
        CurrentUser.IsAuthenticated = false;
        CurrentUser.UserId = null;

        var result = await Executor.ExecuteAsync<ListAccountsHandler, IReadOnlyList<AccountDto>>(
            (handler, ct) => handler.ExecuteAsync(ct));

        Assert.False(result.IsSuccess);
        Assert.True(result.IsBusinessError);
        Assert.Equal(UseCaseExecutor.SessionExpiredMessage, result.ErrorMessage);
        Assert.Contains(Snackbar.ShownSnackbars, item =>
            item.Severity == Severity.Warning && item.Message == UseCaseExecutor.SessionExpiredMessage);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_RethrowsOperationCanceledException()
    {
        Services.AddScoped<ThrowingHandler>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Executor.ExecuteAsync<ThrowingHandler>(
                (handler, ct) => handler.ThrowIfCancelledAsync(ct),
                cancellationToken: cts.Token));
    }

    private sealed class ScopeProbeHandler
    {
        public Guid InstanceId { get; } = Guid.NewGuid();
    }

    private sealed class CurrentUserProbeHandler(Hearth.Application.Abstractions.ICurrentUser currentUser)
    {
        public Guid? CurrentUserId => currentUser.UserId;
    }

    private sealed class ThrowingHandler
    {
        public Task ThrowDomainAsync() =>
            throw new DomainException("Business rule violated.");

        public Task ThrowUnexpectedAsync() =>
            throw new InvalidOperationException("Database socket closed.");

        public Task ThrowIfCancelledAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
