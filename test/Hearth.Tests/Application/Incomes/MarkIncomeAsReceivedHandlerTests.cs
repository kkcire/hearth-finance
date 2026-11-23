using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Application.Incomes.MarkIncomeAsReceived;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Incomes;

public class MarkIncomeAsReceivedHandlerTests
{
    private static readonly DateOnly InitialDate = new(2026, 10, 1);

    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryIncomeRepository _incomes = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly MarkIncomeAsReceivedHandler _handler;

    public MarkIncomeAsReceivedHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new MarkIncomeAsReceivedHandler(_accountAccess, _incomes, _unitOfWork);
    }

    private async Task<Income> SeedIncomeAsync()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta principal",
            Money.FromDecimal(1000.00m),
            InitialDate);
        await _accounts.AddAsync(account);

        var income = Income.Create(
            Guid.NewGuid(),
            account.Id,
            new DateOnly(2026, 10, 28),
            Money.FromDecimal(5000.00m),
            "Salário");
        await _incomes.AddAsync(income);

        return income;
    }

    [Fact]
    public async Task ExecuteAsync_ExpectedIncome_MarksAsReceivedAndSaves()
    {
        var income = await SeedIncomeAsync();

        var changed = await _handler.ExecuteAsync(
            new MarkIncomeAsReceivedCommand(income.Id, new DateOnly(2026, 11, 2)));

        Assert.True(changed);
        Assert.True(income.IsReceived);
        Assert.Equal(new DateOnly(2026, 11, 2), income.ReceivedOn);
        Assert.Equal(1, _incomes.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyReceived_IsIdempotentAndDoesNotSaveAgain()
    {
        var income = await SeedIncomeAsync();
        income.MarkAsReceived(new DateOnly(2026, 10, 28));

        var changed = await _handler.ExecuteAsync(
            new MarkIncomeAsReceivedCommand(income.Id, new DateOnly(2026, 11, 2)));

        Assert.False(changed);
        Assert.Equal(new DateOnly(2026, 10, 28), income.ReceivedOn);
        Assert.Equal(0, _incomes.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ReceivedOnBeforeInitialBalanceDate_ThrowsDomainExceptionAndDoesNotSave()
    {
        var income = await SeedIncomeAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new MarkIncomeAsReceivedCommand(income.Id, InitialDate.AddDays(-1))));

        Assert.False(income.IsReceived);
        Assert.Equal(0, _incomes.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownIncome_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new MarkIncomeAsReceivedCommand(unknownId, new DateOnly(2026, 10, 28))));

        Assert.Equal(nameof(Income), exception.ResourceName);
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
        Assert.Throws<ArgumentNullException>(() => new MarkIncomeAsReceivedHandler(null!, _incomes, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new MarkIncomeAsReceivedHandler(_accountAccess, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new MarkIncomeAsReceivedHandler(_accountAccess, _incomes, null!));
    }
}
