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
    private readonly InMemoryDirectExpenseRepository _directExpenses = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly UpdateDirectExpenseHandler _handler;

    public UpdateDirectExpenseHandlerTests()
    {
        _handler = new UpdateDirectExpenseHandler(_accounts, _directExpenses, _unitOfWork);
    }

    private async Task<(Account Account, DirectExpense Expense)> SeedAsync()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            "Conta principal",
            Money.FromDecimal(1000.00m),
            InitialDate,
            CardBillingRule.Of(3, 12));
        await _accounts.AddAsync(account);

        var expense = DirectExpense.Create(
            Guid.NewGuid(),
            account.Id,
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(50.00m),
            "Mercado",
            ExpenseCategory.Food);
        await _directExpenses.AddAsync(expense);

        return (account, expense);
    }

    [Fact]
    public async Task ExecuteAsync_ValidCommand_UpdatesAllFieldsAndSaves()
    {
        var (_, expense) = await SeedAsync();

        await _handler.ExecuteAsync(new UpdateDirectExpenseCommand(
            expense.Id,
            new DateOnly(2026, 10, 20),
            120.50m,
            "  Farmácia  ",
            ExpenseCategory.Health));

        var saved = await _directExpenses.GetByIdAsync(expense.Id);
        Assert.NotNull(saved);
        Assert.Equal(new DateOnly(2026, 10, 20), saved.Date);
        Assert.Equal(Money.FromDecimal(120.50m), saved.Amount);
        Assert.Equal("Farmácia", saved.Description);
        Assert.Equal(ExpenseCategory.Health, saved.Category);
        Assert.Equal(1, _directExpenses.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownExpense_ThrowsNotFoundAndDoesNotSave()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new UpdateDirectExpenseCommand(
                unknownId, new DateOnly(2026, 10, 20), 10.00m, "Farmácia", ExpenseCategory.Health)));

        Assert.Equal(nameof(DirectExpense), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
        Assert.Equal(0, _directExpenses.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_DateBeforeInitialBalanceDate_ThrowsDomainExceptionAndDoesNotSave()
    {
        var (_, expense) = await SeedAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new UpdateDirectExpenseCommand(
                expense.Id, InitialDate.AddDays(-1), 10.00m, "Farmácia", ExpenseCategory.Health)));

        Assert.Equal(new DateOnly(2026, 10, 10), expense.Date);
        Assert.Equal(0, _directExpenses.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidAmount_ThrowsAndDoesNotSave()
    {
        var (_, expense) = await SeedAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _handler.ExecuteAsync(
            new UpdateDirectExpenseCommand(
                expense.Id, new DateOnly(2026, 10, 20), 0m, "Farmácia", ExpenseCategory.Health)));

        Assert.Equal(0, _directExpenses.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsCancellationToken()
    {
        var (_, expense) = await SeedAsync();
        using var cts = new CancellationTokenSource();

        await _handler.ExecuteAsync(
            new UpdateDirectExpenseCommand(
                expense.Id, new DateOnly(2026, 10, 20), 80.00m, "Farmácia", ExpenseCategory.Health),
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
            () => new UpdateDirectExpenseHandler(null!, _directExpenses, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new UpdateDirectExpenseHandler(_accounts, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new UpdateDirectExpenseHandler(_accounts, _directExpenses, null!));
    }
}
