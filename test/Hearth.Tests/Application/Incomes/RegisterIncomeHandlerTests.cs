using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Application.Incomes.RegisterIncome;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Incomes;

public class RegisterIncomeHandlerTests
{
    private static readonly DateOnly InitialDate = new(2026, 10, 1);

    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryIncomeRepository _incomes = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RegisterIncomeHandler _handler;

    public RegisterIncomeHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new RegisterIncomeHandler(_accountAccess, _incomes, _unitOfWork);
    }

    private async Task<Account> SeedAccountAsync()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta principal",
            Money.FromDecimal(1000.00m),
            InitialDate,
            CardBillingRule.Of(3, 12));
        await _accounts.AddAsync(account);
        return account;
    }

    [Fact]
    public async Task ExecuteAsync_ExpectedIncome_CreatesPersistsAndReturnsId()
    {
        var account = await SeedAccountAsync();

        var id = await _handler.ExecuteAsync(new RegisterIncomeCommand(
            account.Id,
            new DateOnly(2026, 10, 28),
            5000.00m,
            "  Salário  "));

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);

        var saved = await _incomes.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(account.Id, saved.AccountId);
        Assert.Equal(new DateOnly(2026, 10, 28), saved.ExpectedDate);
        Assert.Equal(Money.FromDecimal(5000.00m), saved.Amount);
        Assert.Equal("Salário", saved.Description);
        Assert.False(saved.IsReceived);
        Assert.Null(saved.ReceivedOn);
    }

    [Fact]
    public async Task ExecuteAsync_WithReceivedOn_CreatesAlreadyReceivedInSingleTransaction()
    {
        var account = await SeedAccountAsync();

        var id = await _handler.ExecuteAsync(new RegisterIncomeCommand(
            account.Id,
            new DateOnly(2026, 10, 10),
            250.00m,
            "Pix",
            ReceivedOn: new DateOnly(2026, 10, 12)));

        var saved = await _incomes.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.True(saved.IsReceived);
        Assert.Equal(new DateOnly(2026, 10, 12), saved.ReceivedOn);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ExpectedDateBeforeInitialBalanceDate_ThrowsDomainExceptionAndDoesNotSave()
    {
        var account = await SeedAccountAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new RegisterIncomeCommand(account.Id, InitialDate.AddDays(-1), 100.00m, "Salário")));

        Assert.Empty(_incomes.Incomes);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ReceivedOnBeforeInitialBalanceDate_ThrowsDomainExceptionAndDoesNotSave()
    {
        var account = await SeedAccountAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new RegisterIncomeCommand(
                account.Id, InitialDate, 100.00m, "Pix", ReceivedOn: InitialDate.AddDays(-1))));

        Assert.Empty(_incomes.Incomes);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFoundAndDoesNotSave()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new RegisterIncomeCommand(unknownId, InitialDate, 100.00m, "Salário")));

        Assert.Equal(nameof(Account), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
        Assert.Empty(_incomes.Incomes);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task ExecuteAsync_NonPositiveAmount_ThrowsAndDoesNotSave(double raw)
    {
        var account = await SeedAccountAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _handler.ExecuteAsync(
            new RegisterIncomeCommand(account.Id, InitialDate, (decimal)raw, "Salário")));

        Assert.Empty(_incomes.Incomes);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsCancellationToken()
    {
        var account = await SeedAccountAsync();
        using var cts = new CancellationTokenSource();

        await _handler.ExecuteAsync(
            new RegisterIncomeCommand(account.Id, InitialDate, 100.00m, "Pix"),
            cts.Token);

        Assert.Equal(cts.Token, _accounts.LastCancellationToken);
        Assert.Equal(cts.Token, _incomes.LastCancellationToken);
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
        Assert.Throws<ArgumentNullException>(() => new RegisterIncomeHandler(null!, _incomes, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new RegisterIncomeHandler(_accountAccess, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new RegisterIncomeHandler(_accountAccess, _incomes, null!));
    }
}
