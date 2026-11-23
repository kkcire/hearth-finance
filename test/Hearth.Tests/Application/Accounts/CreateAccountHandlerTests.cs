using Hearth.Application.Accounts.CreateAccount;
using Hearth.Application.Authorization;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Accounts;

public class CreateAccountHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly CreateAccountHandler _handler;

    public CreateAccountHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new CreateAccountHandler(_accountAccess, _accounts, _unitOfWork);
    }

    private static CreateAccountCommand ValidCommand(
        string name = "Conta principal",
        decimal initialBalance = 1500.00m,
        DateOnly? initialBalanceDate = null,
        bool isConfirmed = true) =>
        new(name, initialBalance, initialBalanceDate ?? new DateOnly(2026, 10, 1), isConfirmed);

    [Fact]
    public async Task ExecuteAsync_ValidCommand_CreatesPersistsAndReturnsAccountId()
    {
        var command = ValidCommand(name: "  Nubank  ", initialBalance: 2345.67m);

        var id = await _handler.ExecuteAsync(command);

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);

        var saved = await _accounts.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(id, saved.Id);
        Assert.Equal(FakeCurrentUser.DefaultUserId, saved.OwnerUserId);
        Assert.Equal("Nubank", saved.Name);
        Assert.Equal(Money.FromDecimal(2345.67m), saved.InitialBalance);
        Assert.Equal(new DateOnly(2026, 10, 1), saved.InitialBalanceDate);
        Assert.True(saved.IsInitialBalanceConfirmed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-500.50)]
    public async Task ExecuteAsync_ZeroOrNegativeInitialBalance_IsAccepted(double raw)
    {
        var id = await _handler.ExecuteAsync(ValidCommand(initialBalance: (decimal)raw));

        var saved = await _accounts.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(Money.FromDecimal((decimal)raw), saved.InitialBalance);
    }

    [Fact]
    public async Task ExecuteAsync_NullCommand_ThrowsAndDoesNotSave()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.ExecuteAsync(null!));

        Assert.Empty(_accounts.Accounts);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new CreateAccountHandler(null!, _accounts, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new CreateAccountHandler(_accountAccess, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new CreateAccountHandler(_accountAccess, _accounts, null!));
    }
}
