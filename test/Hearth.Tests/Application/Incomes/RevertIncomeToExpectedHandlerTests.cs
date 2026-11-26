using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Application.Incomes.RevertIncomeToExpected;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Incomes;

public class RevertIncomeToExpectedHandlerTests
{
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryIncomeRepository _incomes = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RevertIncomeToExpectedHandler _handler;

    public RevertIncomeToExpectedHandlerTests()
    {
        _accounts.AddAsync(Account.Create(
            _accountId,
            FakeCurrentUser.DefaultUserId,
            "Conta",
            Money.Zero,
            TestTime.UtcOn(new DateOnly(2026, 1, 1)))).GetAwaiter().GetResult();
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new RevertIncomeToExpectedHandler(_accountAccess, _incomes, _unitOfWork);
    }

    private async Task<Income> SeedIncomeAsync(bool received)
    {
        var income = Income.Create(
            Guid.NewGuid(),
            _accountId,
            new DateOnly(2026, 10, 28),
            Money.FromDecimal(5000.00m),
            "Salário");

        if (received)
        {
            income.MarkAsReceived(new DateOnly(2026, 10, 28), new DateOnly(2026, 12, 31));
        }

        await _incomes.AddAsync(income);
        return income;
    }

    [Fact]
    public async Task ExecuteAsync_ReceivedIncome_RevertsAndSaves()
    {
        var income = await SeedIncomeAsync(received: true);

        var changed = await _handler.ExecuteAsync(new RevertIncomeToExpectedCommand(income.Id));

        Assert.True(changed);
        Assert.False(income.IsReceived);
        Assert.Null(income.ReceivedOn);
        Assert.Equal(1, _incomes.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyExpected_IsIdempotentAndDoesNotSave()
    {
        var income = await SeedIncomeAsync(received: false);

        var changed = await _handler.ExecuteAsync(new RevertIncomeToExpectedCommand(income.Id));

        Assert.False(changed);
        Assert.Equal(0, _incomes.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownIncome_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.ExecuteAsync(new RevertIncomeToExpectedCommand(unknownId)));

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
        Assert.Throws<ArgumentNullException>(() => new RevertIncomeToExpectedHandler(null!, _incomes, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new RevertIncomeToExpectedHandler(_accountAccess, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new RevertIncomeToExpectedHandler(_accountAccess, _incomes, null!));
    }
}
