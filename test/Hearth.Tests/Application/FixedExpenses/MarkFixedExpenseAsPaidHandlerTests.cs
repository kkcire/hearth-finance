using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Application.FixedExpenses.MarkFixedExpenseAsPaid;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.FixedExpenses;

public class MarkFixedExpenseAsPaidHandlerTests
{
    private static readonly DateOnly InitialDate = new(2026, 10, 1);

    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryFixedExpenseRepository _fixedExpenses = new();
    private readonly FakeClock _clock = new(new DateOnly(2026, 11, 20));
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly MarkFixedExpenseAsPaidHandler _handler;

    public MarkFixedExpenseAsPaidHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new MarkFixedExpenseAsPaidHandler(_accountAccess, _fixedExpenses, _clock, _unitOfWork);
    }

    private async Task<FixedExpense> SeedAsync()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta principal",
            Money.FromDecimal(1000.00m),
            InitialDate);
        await _accounts.AddAsync(account);

        var expense = FixedExpense.Create(
            Guid.NewGuid(),
            account.Id,
            YearMonth.Of(2026, 10),
            YearMonth.Of(2026, 12),
            Money.FromDecimal(1000.00m),
            "Aluguel",
            Guid.NewGuid());
        await _fixedExpenses.AddAsync(expense);

        return expense;
    }

    [Fact]
    public async Task ExecuteAsync_ActiveMonth_MarksAsPaidAndSaves()
    {
        var expense = await SeedAsync();

        var changed = await _handler.ExecuteAsync(
            new MarkFixedExpenseAsPaidCommand(expense.Id, 2026, 10, new DateOnly(2026, 10, 5)));

        Assert.True(changed);
        Assert.True(expense.IsPaid(YearMonth.Of(2026, 10)));
        Assert.Equal(new DateOnly(2026, 10, 5), expense.PaidOn(YearMonth.Of(2026, 10)));
        Assert.Equal(1, _fixedExpenses.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_FutureCompetenceMonthPaidToday_Succeeds_AndFuturePaidDateThrows()
    {
        var expense = await SeedAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new MarkFixedExpenseAsPaidCommand(expense.Id, 2026, 11, _clock.Today.AddDays(1))));
        Assert.False(expense.IsPaid(YearMonth.Of(2026, 11)));
        Assert.Equal(0, _unitOfWork.SaveChangesCount);

        var changed = await _handler.ExecuteAsync(
            new MarkFixedExpenseAsPaidCommand(expense.Id, 2026, 12, _clock.Today));

        Assert.True(changed);
        Assert.Equal(_clock.Today, expense.PaidOn(YearMonth.Of(2026, 12)));
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyPaid_IsIdempotentAndDoesNotSaveAgain()
    {
        var expense = await SeedAsync();
        expense.MarkAsPaid(YearMonth.Of(2026, 10), new DateOnly(2026, 10, 5), _clock.Today);

        var changed = await _handler.ExecuteAsync(
            new MarkFixedExpenseAsPaidCommand(expense.Id, 2026, 10, new DateOnly(2026, 10, 20)));

        Assert.False(changed);
        Assert.Equal(new DateOnly(2026, 10, 5), expense.PaidOn(YearMonth.Of(2026, 10)));
        Assert.Equal(0, _fixedExpenses.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_PaidOnBeforeInitialBalanceDate_ThrowsDomainExceptionAndDoesNotSave()
    {
        var expense = await SeedAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new MarkFixedExpenseAsPaidCommand(expense.Id, 2026, 10, InitialDate.AddDays(-1))));

        Assert.False(expense.IsPaid(YearMonth.Of(2026, 10)));
        Assert.Equal(0, _fixedExpenses.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_InactiveMonth_ThrowsDomainExceptionAndDoesNotSave()
    {
        var expense = await SeedAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new MarkFixedExpenseAsPaidCommand(expense.Id, 2027, 1, _clock.Today)));

        Assert.Equal(0, _fixedExpenses.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownExpense_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new MarkFixedExpenseAsPaidCommand(unknownId, 2026, 10, new DateOnly(2026, 10, 5))));

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
            () => new MarkFixedExpenseAsPaidHandler(null!, _fixedExpenses, _clock, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new MarkFixedExpenseAsPaidHandler(_accountAccess, null!, _clock, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new MarkFixedExpenseAsPaidHandler(_accountAccess, _fixedExpenses, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(
            () => new MarkFixedExpenseAsPaidHandler(_accountAccess, _fixedExpenses, _clock, null!));
    }
}
