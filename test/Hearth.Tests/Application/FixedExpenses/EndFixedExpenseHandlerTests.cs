using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Application.FixedExpenses.EndFixedExpense;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.FixedExpenses;

public class EndFixedExpenseHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryFixedExpenseRepository _fixedExpenses = new();
    private readonly FakeClock _clock = new(new DateOnly(2026, 10, 10));
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly EndFixedExpenseHandler _handler;

    public EndFixedExpenseHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new EndFixedExpenseHandler(_accountAccess, _fixedExpenses, _clock, _unitOfWork);
    }

    private async Task<FixedExpense> SeedAsync()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta principal",
            Money.FromDecimal(1000.00m),
            TestTime.UtcOn(new DateOnly(2026, 1, 1)));
        await _accounts.AddAsync(account);

        var expense = FixedExpense.Create(
            Guid.NewGuid(),
            account.Id,
            YearMonth.Of(2026, 1),
            null,
            Money.FromDecimal(1000.00m),
            "Aluguel",
            Guid.NewGuid());
        await _fixedExpenses.AddAsync(expense);

        return expense;
    }

    [Fact]
    public async Task ExecuteAsync_OpenMonthBoundary_SetsEndMonthAndSaves()
    {
        // Today is 2026-10-10 -> first open calendar month is October, so ending at September stops October onward.
        var expense = await SeedAsync();

        await _handler.ExecuteAsync(new EndFixedExpenseCommand(expense.Id, 2026, 9));

        Assert.Equal(YearMonth.Of(2026, 9), expense.EndMonth);
        Assert.Equal(1, _fixedExpenses.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_WouldStopFrozenMonth_ThrowsDomainExceptionAndDoesNotSave()
    {
        // Today is 2026-10-10 -> September is frozen, so ending at August would stop September and is rejected.
        var expense = await SeedAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new EndFixedExpenseCommand(expense.Id, 2026, 8)));

        Assert.Null(expense.EndMonth);
        Assert.Equal(0, _fixedExpenses.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownExpense_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new EndFixedExpenseCommand(unknownId, 2026, 12)));

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
            () => new EndFixedExpenseHandler(null!, _fixedExpenses, _clock, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new EndFixedExpenseHandler(_accountAccess, null!, _clock, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new EndFixedExpenseHandler(_accountAccess, _fixedExpenses, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new EndFixedExpenseHandler(_accountAccess, _fixedExpenses, _clock, null!));
    }
}
