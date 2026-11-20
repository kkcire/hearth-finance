using Hearth.Application.Accounts.CreateAccount;
using Hearth.Application.Categories;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Accounts;

public class CreateAccountHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly CreateAccountHandler _handler;

    public CreateAccountHandlerTests()
    {
        _handler = new CreateAccountHandler(_accounts, _categories, _unitOfWork);
    }

    private static CreateAccountCommand ValidCommand(
        string name = "Conta principal",
        decimal initialBalance = 1500.00m,
        DateOnly? initialBalanceDate = null,
        int closingDay = 3,
        int dueDay = 12) =>
        new(name, initialBalance, initialBalanceDate ?? new DateOnly(2026, 10, 1), closingDay, dueDay);

    [Fact]
    public async Task ExecuteAsync_ValidCommand_CreatesPersistsAndReturnsAccountId()
    {
        var command = ValidCommand(name: "  Nubank  ", initialBalance: 2345.67m, closingDay: 3, dueDay: 12);

        var id = await _handler.ExecuteAsync(command);

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);

        var saved = await _accounts.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(id, saved.Id);
        Assert.Equal("Nubank", saved.Name);
        Assert.Equal(Money.FromDecimal(2345.67m), saved.InitialBalance);
        Assert.Equal(new DateOnly(2026, 10, 1), saved.InitialBalanceDate);
        Assert.Equal(CardBillingRule.Of(3, 12), saved.BillingRule);

        var seededCategories = await _categories.GetByAccountAsync(id);
        Assert.Equal(DefaultCategories.Names.Count, seededCategories.Count);
        Assert.Equal(
            DefaultCategories.Names.OrderBy(n => n),
            seededCategories.Select(c => c.Name).OrderBy(n => n));
        Assert.All(seededCategories, c => Assert.False(c.IsArchived));
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

    [Theory]
    [InlineData(2020, 1, 1)]
    [InlineData(2026, 11, 20)]
    [InlineData(2035, 12, 31)]
    public async Task ExecuteAsync_AnyInitialBalanceDate_IsAccepted(int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);

        var id = await _handler.ExecuteAsync(ValidCommand(initialBalanceDate: date));

        var saved = await _accounts.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(date, saved.InitialBalanceDate);
    }

    [Fact]
    public async Task ExecuteAsync_SameNameTwice_CreatesTwoDistinctAccounts()
    {
        var firstId = await _handler.ExecuteAsync(ValidCommand(name: "Conta principal"));
        var secondId = await _handler.ExecuteAsync(ValidCommand(name: "Conta principal"));

        Assert.NotEqual(firstId, secondId);
        Assert.Equal(2, _accounts.Accounts.Count);
        Assert.Equal(2, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_NullCommand_ThrowsAndDoesNotSave()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.ExecuteAsync(null!));

        Assert.Empty(_accounts.Accounts);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_InvalidName_ThrowsAndDoesNotSave(string? name)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => _handler.ExecuteAsync(ValidCommand(name: name!)));

        Assert.Empty(_accounts.Accounts);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_InitialBalanceWithMoreThanTwoDecimalPlaces_ThrowsAndDoesNotSave()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.ExecuteAsync(ValidCommand(initialBalance: 10.005m)));

        Assert.Empty(_accounts.Accounts);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(29, 12)]
    [InlineData(3, 0)]
    [InlineData(3, 29)]
    public async Task ExecuteAsync_InvalidBillingRuleDays_ThrowsAndDoesNotSave(int closingDay, int dueDay)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _handler.ExecuteAsync(ValidCommand(closingDay: closingDay, dueDay: dueDay)));

        Assert.Empty(_accounts.Accounts);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsCancellationToken()
    {
        using var cts = new CancellationTokenSource();

        await _handler.ExecuteAsync(ValidCommand(), cts.Token);

        Assert.Equal(cts.Token, _accounts.LastCancellationToken);
        Assert.Equal(cts.Token, _unitOfWork.LastCancellationToken);
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new CreateAccountHandler(null!, _categories, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new CreateAccountHandler(_accounts, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new CreateAccountHandler(_accounts, _categories, null!));
    }
}
