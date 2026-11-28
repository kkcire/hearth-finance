using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Application.Incomes.UpdateIncome;
using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Incomes;

public class UpdateIncomeHandlerTests
{
    private static readonly DateOnly InitialDate = new(2026, 10, 1);

    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryPurchaseConditionRepository _conditions = new();
    private readonly InMemoryIncomeRepository _incomes = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly UpdateIncomeHandler _handler;

    public UpdateIncomeHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new UpdateIncomeHandler(_accountAccess, _incomes, _conditions, _unitOfWork);
    }

    private async Task<(Account Account, Income Income)> SeedAsync(DateOnly? date = null)
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta principal",
            Money.FromDecimal(1000.00m),
            TestTime.UtcOn(InitialDate));
        await _accounts.AddAsync(account);

        var income = Income.Create(
            Guid.NewGuid(),
            account.Id,
            date ?? new DateOnly(2026, 10, 28),
            Money.FromDecimal(5000.00m),
            "Salário");

        await _incomes.AddAsync(income);

        return (account, income);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingIncome_UpdatesAllFieldsFreelyEvenWhenDateIsInThePast()
    {
        var (_, income) = await SeedAsync(date: new DateOnly(2026, 10, 28));

        await _handler.ExecuteAsync(new UpdateIncomeCommand(
            income.Id,
            new DateOnly(2026, 11, 5),
            5500.25m,
            "  Salário atualizado  "));

        var saved = await _incomes.GetByIdAsync(income.Id);
        Assert.NotNull(saved);
        Assert.Equal(new DateOnly(2026, 11, 5), saved.Date);
        Assert.Equal(Money.FromDecimal(5500.25m), saved.Amount);
        Assert.Equal("Salário atualizado", saved.Description);
        Assert.Equal(1, _incomes.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownIncome_ThrowsNotFoundAndDoesNotSave()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new UpdateIncomeCommand(unknownId, new DateOnly(2026, 11, 5), 100.00m, "Pix")));

        Assert.Equal(nameof(Income), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
        Assert.Equal(0, _incomes.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_DateBeforeInitialBalanceDate_ThrowsDomainExceptionAndDoesNotSave()
    {
        var (_, income) = await SeedAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new UpdateIncomeCommand(income.Id, InitialDate.AddDays(-1), 5000.00m, "Salário")));

        Assert.Equal(new DateOnly(2026, 10, 28), income.Date);
        Assert.Equal(0, _incomes.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidAmount_ThrowsAndDoesNotSave()
    {
        var (_, income) = await SeedAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _handler.ExecuteAsync(
            new UpdateIncomeCommand(income.Id, new DateOnly(2026, 11, 5), 0m, "Salário")));

        Assert.Equal(0, _incomes.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_NullCommand_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.ExecuteAsync(null!));
    }

    [Fact]
    public async Task ExecuteAsync_UpdatesConditionWithoutAffectingOtherFields()
    {
        var (_, income) = await SeedAsync();
        var pai = PurchaseCondition.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Pai");
        await _conditions.AddAsync(pai);

        await _handler.ExecuteAsync(new UpdateIncomeCommand(
            income.Id,
            income.Date,
            income.Amount.Amount,
            income.Description,
            ConditionId: pai.Id));

        Assert.Equal(pai.Id, income.ConditionId);
        Assert.False(income.IsOwn);
        Assert.Equal(1, _incomes.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new UpdateIncomeHandler(null!, _incomes, _conditions, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new UpdateIncomeHandler(_accountAccess, null!, _conditions, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new UpdateIncomeHandler(_accountAccess, _incomes, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new UpdateIncomeHandler(_accountAccess, _incomes, _conditions, null!));
    }
}
