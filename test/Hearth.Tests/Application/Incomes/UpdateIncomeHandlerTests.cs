using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Application.Incomes.UpdateIncome;
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
    private readonly InMemoryIncomeRepository _incomes = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly UpdateIncomeHandler _handler;

    public UpdateIncomeHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new UpdateIncomeHandler(_accountAccess, _incomes, _unitOfWork);
    }

    private async Task<(Account Account, Income Income)> SeedAsync(bool received = false)
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

        if (received)
        {
            income.MarkAsReceived(new DateOnly(2026, 10, 30), new DateOnly(2026, 11, 20));
        }

        await _incomes.AddAsync(income);

        return (account, income);
    }

    [Fact]
    public async Task ExecuteAsync_ExpectedIncome_UpdatesAllFieldsAndSaves()
    {
        var (_, income) = await SeedAsync(received: false);

        await _handler.ExecuteAsync(new UpdateIncomeCommand(
            income.Id,
            new DateOnly(2026, 11, 5),
            5500.25m,
            "  Salário atualizado  "));

        var saved = await _incomes.GetByIdAsync(income.Id);
        Assert.NotNull(saved);
        Assert.Equal(new DateOnly(2026, 11, 5), saved.ExpectedDate);
        Assert.Equal(Money.FromDecimal(5500.25m), saved.Amount);
        Assert.Equal("Salário atualizado", saved.Description);
        Assert.False(saved.IsReceived);
        Assert.Equal(1, _incomes.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ReceivedIncome_AllowsDescriptionChangeOnly_AndBlocksAmountOrExpectedDateChanges()
    {
        var (_, income) = await SeedAsync(received: true);

        await _handler.ExecuteAsync(new UpdateIncomeCommand(
            income.Id,
            income.ExpectedDate,
            income.Amount.Amount,
            "  Salário outubro  "));

        Assert.Equal("Salário outubro", income.Description);
        Assert.True(income.IsReceived);
        Assert.Equal(1, _incomes.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(new UpdateIncomeCommand(
            income.Id,
            income.ExpectedDate,
            5500.25m,
            "Salário outubro")));

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(new UpdateIncomeCommand(
            income.Id,
            new DateOnly(2026, 11, 5),
            income.Amount.Amount,
            "Salário outubro")));

        Assert.Equal(Money.FromDecimal(5000.00m), income.Amount);
        Assert.Equal(new DateOnly(2026, 10, 28), income.ExpectedDate);
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
    public async Task ExecuteAsync_ExpectedDateBeforeInitialBalanceDate_ThrowsDomainExceptionAndDoesNotSave()
    {
        var (_, income) = await SeedAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new UpdateIncomeCommand(income.Id, InitialDate.AddDays(-1), 5000.00m, "Salário")));

        Assert.Equal(new DateOnly(2026, 10, 28), income.ExpectedDate);
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
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new UpdateIncomeHandler(null!, _incomes, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new UpdateIncomeHandler(_accountAccess, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new UpdateIncomeHandler(_accountAccess, _incomes, null!));
    }
}
