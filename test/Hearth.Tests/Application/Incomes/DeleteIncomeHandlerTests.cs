using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Application.Incomes.DeleteIncome;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Incomes;

public class DeleteIncomeHandlerTests
{
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryIncomeRepository _incomes = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly DeleteIncomeHandler _handler;

    public DeleteIncomeHandlerTests()
    {
        _accounts.AddAsync(Account.Create(
            _accountId,
            FakeCurrentUser.DefaultUserId,
            "Conta",
            Money.Zero,
            new DateOnly(2026, 1, 1))).GetAwaiter().GetResult();
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new DeleteIncomeHandler(_accountAccess, _incomes, _unitOfWork);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_ExistingIncome_DeletesWhetherExpectedOrReceived(bool received)
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

        await _handler.ExecuteAsync(new DeleteIncomeCommand(income.Id));

        Assert.Null(await _incomes.GetByIdAsync(income.Id));
        Assert.Equal(1, _incomes.DeleteCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownIncome_ThrowsNotFoundAndDoesNotSave()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.ExecuteAsync(new DeleteIncomeCommand(unknownId)));

        Assert.Equal(nameof(Income), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
        Assert.Equal(0, _incomes.DeleteCount);
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
        Assert.Throws<ArgumentNullException>(() => new DeleteIncomeHandler(null!, _incomes, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new DeleteIncomeHandler(_accountAccess, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new DeleteIncomeHandler(_accountAccess, _incomes, null!));
    }
}
