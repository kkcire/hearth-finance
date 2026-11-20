using Hearth.Application.DirectExpenses.RegisterDirectExpense;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.DirectExpenses;

public class RegisterDirectExpenseHandlerTests
{
    private static readonly DateOnly InitialDate = new(2026, 10, 1);

    private readonly InMemoryAccountRepository _accounts = new();
    private readonly InMemoryDirectExpenseRepository _directExpenses = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RegisterDirectExpenseHandler _handler;

    public RegisterDirectExpenseHandlerTests()
    {
        _handler = new RegisterDirectExpenseHandler(_accounts, _directExpenses, _unitOfWork);
    }

    private async Task<Account> SeedAccountAsync()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            "Conta principal",
            Money.FromDecimal(1000.00m),
            InitialDate,
            CardBillingRule.Of(3, 12));
        await _accounts.AddAsync(account);
        return account;
    }

    [Fact]
    public async Task ExecuteAsync_ValidCommand_CreatesPersistsAndReturnsId()
    {
        var account = await SeedAccountAsync();

        var id = await _handler.ExecuteAsync(new RegisterDirectExpenseCommand(
            account.Id,
            new DateOnly(2026, 10, 15),
            85.40m,
            "  Mercado  ",
            ExpenseCategory.Food));

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);

        var saved = await _directExpenses.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(account.Id, saved.AccountId);
        Assert.Equal(new DateOnly(2026, 10, 15), saved.Date);
        Assert.Equal(Money.FromDecimal(85.40m), saved.Amount);
        Assert.Equal("Mercado", saved.Description);
        Assert.Equal(ExpenseCategory.Food, saved.Category);
    }

    [Fact]
    public async Task ExecuteAsync_OnInitialBalanceDate_IsAccepted()
    {
        var account = await SeedAccountAsync();

        var id = await _handler.ExecuteAsync(new RegisterDirectExpenseCommand(
            account.Id, InitialDate, 20.00m, "Café", ExpenseCategory.Food));

        Assert.NotNull(await _directExpenses.GetByIdAsync(id));
    }

    [Fact]
    public async Task ExecuteAsync_BeforeInitialBalanceDate_ThrowsDomainExceptionAndDoesNotSave()
    {
        var account = await SeedAccountAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new RegisterDirectExpenseCommand(
                account.Id, InitialDate.AddDays(-1), 20.00m, "Café", ExpenseCategory.Food)));

        Assert.Empty(_directExpenses.Expenses);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFoundAndDoesNotSave()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new RegisterDirectExpenseCommand(
                unknownId, InitialDate, 20.00m, "Café", ExpenseCategory.Food)));

        Assert.Equal(nameof(Account), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
        Assert.Empty(_directExpenses.Expenses);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task ExecuteAsync_NonPositiveAmount_ThrowsAndDoesNotSave(double raw)
    {
        var account = await SeedAccountAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _handler.ExecuteAsync(
            new RegisterDirectExpenseCommand(
                account.Id, InitialDate, (decimal)raw, "Café", ExpenseCategory.Food)));

        Assert.Empty(_directExpenses.Expenses);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_AmountWithMoreThanTwoDecimalPlaces_ThrowsAndDoesNotSave()
    {
        var account = await SeedAccountAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.ExecuteAsync(
            new RegisterDirectExpenseCommand(
                account.Id, InitialDate, 10.005m, "Café", ExpenseCategory.Food)));

        Assert.Empty(_directExpenses.Expenses);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsCancellationToken()
    {
        var account = await SeedAccountAsync();
        using var cts = new CancellationTokenSource();

        await _handler.ExecuteAsync(
            new RegisterDirectExpenseCommand(account.Id, InitialDate, 15.00m, "Café", ExpenseCategory.Food),
            cts.Token);

        Assert.Equal(cts.Token, _accounts.LastCancellationToken);
        Assert.Equal(cts.Token, _directExpenses.LastCancellationToken);
        Assert.Equal(cts.Token, _unitOfWork.LastCancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_NullCommand_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.ExecuteAsync(null!));
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RegisterDirectExpenseHandler(null!, _directExpenses, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new RegisterDirectExpenseHandler(_accounts, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new RegisterDirectExpenseHandler(_accounts, _directExpenses, null!));
    }
}
