using Hearth.Application.Exceptions;
using Hearth.Application.FixedExpenses.ChangeFixedExpenseAmount;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.FixedExpenses;

public class ChangeFixedExpenseAmountHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly InMemoryFixedExpenseRepository _fixedExpenses = new();
    private readonly FakeClock _clock = new(new DateOnly(2026, 10, 10));
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly ChangeFixedExpenseAmountHandler _handler;

    public ChangeFixedExpenseAmountHandlerTests()
    {
        _handler = new ChangeFixedExpenseAmountHandler(_accounts, _fixedExpenses, _clock, _unitOfWork);
    }

    private async Task<FixedExpense> SeedAsync(YearMonth? end = null)
    {
        var account = Account.Create(
            Guid.NewGuid(),
            "Conta principal",
            Money.FromDecimal(1000.00m),
            new DateOnly(2026, 1, 1),
            CardBillingRule.Of(3, 12));
        await _accounts.AddAsync(account);

        var expense = FixedExpense.Create(
            Guid.NewGuid(),
            account.Id,
            YearMonth.Of(2026, 1),
            end,
            Money.FromDecimal(1000.00m),
            "Aluguel",
            ExpenseCategory.Services);
        await _fixedExpenses.AddAsync(expense);

        return expense;
    }

    [Fact]
    public async Task ExecuteAsync_AfterClosingDay_AppliesFromNextMonthAndSaves()
    {
        // Today is 2026-10-10 and closing day is 3 -> effective from November.
        var expense = await SeedAsync();

        var result = await _handler.ExecuteAsync(
            new ChangeFixedExpenseAmountCommand(expense.Id, 1250.00m));

        Assert.Equal(2026, result.EffectiveFromYear);
        Assert.Equal(11, result.EffectiveFromMonth);
        Assert.Equal(Money.FromDecimal(1000.00m), expense.AmountIn(YearMonth.Of(2026, 10)));
        Assert.Equal(Money.FromDecimal(1250.00m), expense.AmountIn(YearMonth.Of(2026, 11)));
        Assert.Equal(1, _fixedExpenses.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_OnOrBeforeClosingDay_AppliesFromCurrentMonth()
    {
        _clock.Today = new DateOnly(2026, 10, 3);
        var expense = await SeedAsync();

        var result = await _handler.ExecuteAsync(
            new ChangeFixedExpenseAmountCommand(expense.Id, 1250.00m));

        Assert.Equal(2026, result.EffectiveFromYear);
        Assert.Equal(10, result.EffectiveFromMonth);
        Assert.Equal(Money.FromDecimal(1250.00m), expense.AmountIn(YearMonth.Of(2026, 10)));
    }

    [Fact]
    public async Task ExecuteAsync_AffectedMonthAlreadyPaid_ThrowsDomainExceptionAndDoesNotSave()
    {
        _clock.Today = new DateOnly(2026, 10, 2);
        var expense = await SeedAsync();
        expense.MarkAsPaid(YearMonth.Of(2026, 10), new DateOnly(2026, 10, 1));

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new ChangeFixedExpenseAmountCommand(expense.Id, 1250.00m)));

        Assert.Equal(0, _fixedExpenses.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownExpense_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new ChangeFixedExpenseAmountCommand(unknownId, 1250.00m)));

        Assert.Equal(nameof(FixedExpense), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
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
            () => new ChangeFixedExpenseAmountHandler(null!, _fixedExpenses, _clock, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new ChangeFixedExpenseAmountHandler(_accounts, null!, _clock, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new ChangeFixedExpenseAmountHandler(_accounts, _fixedExpenses, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new ChangeFixedExpenseAmountHandler(_accounts, _fixedExpenses, _clock, null!));
    }
}
