using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Application.Incomes.RegisterIncome;
using Hearth.Domain;
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
    private readonly InMemoryPurchaseConditionRepository _conditions = new();
    private readonly InMemoryIncomeRepository _incomes = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RegisterIncomeHandler _handler;

    public RegisterIncomeHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new RegisterIncomeHandler(_accountAccess, _incomes, _conditions, _unitOfWork);
    }

    private async Task<Account> SeedAccountAsync()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta principal",
            Money.FromDecimal(1000.00m),
            TestTime.UtcOn(InitialDate));
        await _accounts.AddAsync(account);
        return account;
    }

    [Fact]
    public async Task ExecuteAsync_ValidIncome_CreatesPersistsAndReturnsId()
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
        Assert.Equal(new DateOnly(2026, 10, 28), saved.Date);
        Assert.Equal(YearMonth.Of(2026, 10), saved.Month);
        Assert.Equal(Money.FromDecimal(5000.00m), saved.Amount);
        Assert.Equal("Salário", saved.Description);
        Assert.True(saved.IsReceivedOn(new DateOnly(2026, 10, 28)));
        Assert.False(saved.IsReceivedOn(new DateOnly(2026, 10, 27)));
    }

    [Fact]
    public async Task ExecuteAsync_FutureDate_PersistsAsExpectedRelativeToToday()
    {
        var account = await SeedAccountAsync();

        var id = await _handler.ExecuteAsync(new RegisterIncomeCommand(
            account.Id,
            new DateOnly(2026, 12, 5),
            2500.00m,
            "Salário Dez"));

        var saved = await _incomes.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(new DateOnly(2026, 12, 5), saved.Date);
        Assert.False(saved.IsReceivedOn(new DateOnly(2026, 11, 20)));
        Assert.True(saved.IsReceivedOn(new DateOnly(2026, 12, 5)));
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_DateBeforeInitialBalanceDate_ThrowsDomainExceptionAndDoesNotSave()
    {
        var account = await SeedAccountAsync();

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(
            new RegisterIncomeCommand(account.Id, InitialDate.AddDays(-1), 100.00m, "Salário")));

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
    public async Task ExecuteAsync_WithCustomCondition_PersistsConditionId_AndRejectsArchivedCondition()
    {
        var account = await SeedAccountAsync();
        var pai = PurchaseCondition.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Pai");
        var archived = PurchaseCondition.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Antiga");
        archived.Archive();
        await _conditions.AddAsync(pai);
        await _conditions.AddAsync(archived);

        var id = await _handler.ExecuteAsync(new RegisterIncomeCommand(
            account.Id,
            new DateOnly(2026, 10, 15),
            950.00m,
            "Reembolso fatura",
            ConditionId: pai.Id));

        var saved = await _incomes.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(pai.Id, saved.ConditionId);
        Assert.False(saved.IsOwn);

        await Assert.ThrowsAsync<DomainException>(() => _handler.ExecuteAsync(new RegisterIncomeCommand(
            account.Id,
            new DateOnly(2026, 10, 15),
            100.00m,
            "Arquivada",
            ConditionId: archived.Id)));
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new RegisterIncomeHandler(null!, _incomes, _conditions, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new RegisterIncomeHandler(_accountAccess, null!, _conditions, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new RegisterIncomeHandler(_accountAccess, _incomes, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new RegisterIncomeHandler(_accountAccess, _incomes, _conditions, null!));
    }
}
