using Bunit;
using Hearth.Application;
using Hearth.Application.Abstractions;
using Hearth.Application.Accounts.CreateAccount;
using Hearth.Application.CreditCards.CreateCreditCard;
using Hearth.Web.Auth;
using Hearth.Web.Services;
using Hearth.Tests.Application.Fakes;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MudBlazor;
using MudBlazor.Services;

namespace Hearth.Tests.Web;

/// <summary>
/// Base class for bUnit component and Web service tests.
/// Registers in-memory repositories and clock as Singletons per test instance so state
/// persists across the short-lived scopes created by <see cref="UseCaseExecutor"/> while
/// staying completely isolated between tests.
/// </summary>
public abstract class WebTestBase : BunitContext, IAsyncLifetime
{
    internal FakeCurrentUser CurrentUser { get; } = new();
    internal TestAuthenticationStateProvider AuthStateProvider { get; }
    internal HearthAuthOptions AuthOptions { get; } = new() { AllowRegistration = true };
    internal InMemoryUserRepository Users { get; } = new();
    internal FakePasswordHasher PasswordHasher { get; } = new();
    internal InMemoryAccountRepository Accounts { get; } = new();
    internal InMemoryCategoryRepository Categories { get; } = new();
    internal InMemoryPurchaseConditionRepository PurchaseConditions { get; } = new();
    internal InMemoryCategoryBudgetRepository CategoryBudgets { get; } = new();
    internal InMemoryDirectExpenseRepository DirectExpenses { get; } = new();
    internal InMemoryFixedExpenseRepository FixedExpenses { get; } = new();
    internal InMemoryCreditCardRepository CreditCards { get; } = new();
    internal InMemoryCreditCardPurchaseRepository CreditCardPurchases { get; } = new();
    internal InMemoryIncomeRepository Incomes { get; } = new();
    internal FakeUnitOfWork UnitOfWork { get; } = new();
    internal FakeClock Clock { get; } = new(new DateOnly(2026, 11, 20));

    protected WebTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        AuthStateProvider = new TestAuthenticationStateProvider(CurrentUser);

        Services.AddSingleton<AuthenticationStateProvider>(AuthStateProvider);
        Services.AddSingleton<IOptions<HearthAuthOptions>>(_ => Options.Create(AuthOptions));
        Services.AddScoped<ScopedCurrentUser>();
        Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<ScopedCurrentUser>());
        Services.AddSingleton<IUserRepository>(Users);
        Services.AddSingleton<IPasswordHasher>(PasswordHasher);
        Services.AddSingleton<IAccountRepository>(Accounts);
        Services.AddSingleton<ICategoryRepository>(Categories);
        Services.AddSingleton<IPurchaseConditionRepository>(PurchaseConditions);
        Services.AddSingleton<ICategoryBudgetRepository>(CategoryBudgets);
        Services.AddSingleton<IDirectExpenseRepository>(DirectExpenses);
        Services.AddSingleton<IFixedExpenseRepository>(FixedExpenses);
        Services.AddSingleton<ICreditCardRepository>(CreditCards);
        Services.AddSingleton<ICreditCardPurchaseRepository>(CreditCardPurchases);
        Services.AddSingleton<IIncomeRepository>(Incomes);
        Services.AddSingleton<IUnitOfWork>(UnitOfWork);
        Services.AddSingleton<IClock>(Clock);

        Services.AddApplication();

        Services.AddMudServices(options =>
        {
            options.SnackbarConfiguration.ShowTransitionDuration = 0;
            options.SnackbarConfiguration.HideTransitionDuration = 0;
        });

        Services.AddScoped<UseCaseExecutor>();
        Services.AddScoped<AccountContext>();
    }

    protected AccountContext AccountContext => Services.GetRequiredService<AccountContext>();

    protected UseCaseExecutor Executor => Services.GetRequiredService<UseCaseExecutor>();

    protected ISnackbar Snackbar => Services.GetRequiredService<ISnackbar>();

    protected async Task<Guid> SeedAccountAsync(
        string name = "Conta Principal",
        decimal initialBalance = 1000m,
        DateOnly? initialBalanceDate = null,
        bool initializeContext = true)
    {
        var date = initialBalanceDate ?? new DateOnly(2026, 1, 1);
        var createdAtUtc = new DateTimeOffset(date.ToDateTime(new TimeOnly(15, 0)), TimeSpan.Zero);
        var account = Hearth.Domain.Entities.Account.Create(
            Guid.CreateVersion7(),
            CurrentUser.UserId!.Value,
            name,
            Hearth.Domain.ValueObjects.Money.FromDecimal(initialBalance),
            createdAtUtc,
            isInitialBalanceConfirmed: true);

        await Accounts.AddAsync(account);

        if (initializeContext)
        {
            await AccountContext.ReloadAsync(account.Id);
        }

        return account.Id;
    }

    protected async Task<Guid> SeedCreditCardAsync(
        Guid accountId,
        int closingDay = 3,
        int dueDay = 12,
        string? name = "Cartão Principal",
        decimal? limit = 5000m)
    {
        var result = await Executor.ExecuteAsync<CreateCreditCardHandler, Guid>(
            (handler, ct) => handler.ExecuteAsync(
                new CreateCreditCardCommand(accountId, closingDay, dueDay, name, limit),
                ct),
            showErrorSnackbar: false);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        return result.Value;
    }

    public virtual Task InitializeAsync() => Task.CompletedTask;

    public new virtual async Task DisposeAsync() => await base.DisposeAsync();
}
