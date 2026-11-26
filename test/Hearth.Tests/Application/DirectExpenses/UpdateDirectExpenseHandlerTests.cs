using Hearth.Application.Authorization;
using Hearth.Application.DirectExpenses.UpdateDirectExpense;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.DirectExpenses;

public class UpdateDirectExpenseHandlerTests
{
    private static readonly DateOnly InitialDate = new(2026, 10, 1);

    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly InMemoryDirectExpenseRepository _directExpenses = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly UpdateDirectExpenseHandler _handler;

    public UpdateDirectExpenseHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new UpdateDirectExpenseHandler(_accountAccess, _categories, _directExpenses, _unitOfWork);
    }

    private async Task<(Account Account, Category Food, Category Health, DirectExpense Expense)> SeedAsync()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta principal",
            Money.FromDecimal(1000.00m),
            TestTime.UtcOn(InitialDate));
        await _accounts.AddAsync(account);

        var food = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Mercado");
        var health = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Farmácia");
        await _categories.AddAsync(food);
        await _categories.AddAsync(health);

        var expense = DirectExpense.Create(
            Guid.NewGuid(),
            account.Id,
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(50.00m),
            "Mercado",
            food.Id);
        await _directExpenses.AddAsync(expense);

        return (account, food, health, expense);
    }

    [Fact]
    public async Task ExecuteAsync_ValidCommand_UpdatesAllFieldsAndSaves()
    {
        var (_, _, health, expense) = await SeedAsync();

        await _handler.ExecuteAsync(new UpdateDirectExpenseCommand(
            expense.Id,
            new DateOnly(2026, 10, 20),
            120.50m,
            "  Farmácia  ",
            health.Id));

        var saved = await _directExpenses.GetByIdAsync(expense.Id);
        Assert.NotNull(saved);
        Assert.Equal(new DateOnly(2026, 10, 20), saved.Date);
        Assert.Equal(Money.FromDecimal(120.50m), saved.Amount);
        Assert.Equal("Farmácia", saved.Description);
        Assert.Equal(health.Id, saved.CategoryId);
        Assert.Equal(1, _directExpenses.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_KeepingArchivedCategory_AllowsEditingOtherFields()
    {
        var (_, food, _, expense) = await SeedAsync();
        food.Archive();

        await _handler.ExecuteAsync(new UpdateDirectExpenseCommand(
            expense.Id,
            new DateOnly(2026, 10, 20),
            80.00m,
            "Padaria",
            food.Id));

        Assert.Equal("Padaria", expense.Description);
        Assert.Equal(food.Id, expense.CategoryId);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ReassigningToArchivedCategory_ThrowsDomainExceptionAndDoesNotSave()
    {
        var (_, _, health, expense) = await SeedAsync();
        health.Archive();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new UpdateDirectExpenseCommand(
                expense.Id, new DateOnly(2026, 10, 20), 80.00m, "Farmácia", health.Id)));

        Assert.Equal(0, _directExpenses.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownExpense_ThrowsNotFoundAndDoesNotSave()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new UpdateDirectExpenseCommand(
                unknownId, new DateOnly(2026, 10, 20), 10.00m, "Farmácia", Guid.NewGuid())));

        Assert.Equal(nameof(DirectExpense), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
        Assert.Equal(0, _directExpenses.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_DateBeforeInitialBalanceDate_ThrowsDomainExceptionAndDoesNotSave()
    {
        var (_, _, health, expense) = await SeedAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new UpdateDirectExpenseCommand(
                expense.Id, InitialDate.AddDays(-1), 10.00m, "Farmácia", health.Id)));

        Assert.Equal(new DateOnly(2026, 10, 10), expense.Date);
        Assert.Equal(0, _directExpenses.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidAmount_ThrowsAndDoesNotSave()
    {
        var (_, _, health, expense) = await SeedAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _handler.ExecuteAsync(
            new UpdateDirectExpenseCommand(
                expense.Id, new DateOnly(2026, 10, 20), 0m, "Farmácia", health.Id)));

        Assert.Equal(0, _directExpenses.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsCancellationToken()
    {
        var (_, _, health, expense) = await SeedAsync();
        using var cts = new CancellationTokenSource();

        await _handler.ExecuteAsync(
            new UpdateDirectExpenseCommand(
                expense.Id, new DateOnly(2026, 10, 20), 80.00m, "Farmácia", health.Id),
            cts.Token);

        Assert.Equal(cts.Token, _accounts.LastCancellationToken);
        Assert.Equal(cts.Token, _categories.LastCancellationToken);
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
            () => new UpdateDirectExpenseHandler(null!, _categories, _directExpenses, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new UpdateDirectExpenseHandler(_accountAccess, null!, _directExpenses, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new UpdateDirectExpenseHandler(_accountAccess, _categories, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new UpdateDirectExpenseHandler(_accountAccess, _categories, _directExpenses, null!));
    }
}
