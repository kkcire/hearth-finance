using Hearth.Application.Auth;
using Hearth.Application.Auth.RegisterUser;
using Hearth.Domain;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Auth;

public sealed class RegisterUserHandlerTests
{
    private readonly InMemoryUserRepository _users = new();
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new(
        new DateOnly(2026, 11, 22),
        new DateTimeOffset(2026, 11, 22, 15, 0, 0, TimeSpan.Zero));

    private RegisterUserHandler CreateHandler() =>
        new(_users, _accounts, _hasher, _unitOfWork, _clock);

    [Fact]
    public async Task ExecuteAsync_WithValidInput_CreatesUserAndUnconfirmedDefaultAccountInSingleTransaction()
    {
        var handler = CreateHandler();

        var result = await handler.ExecuteAsync(
            new RegisterUserCommand("  Erick  ", "  Erick@Hearth.App  ", "segredo123"));

        Assert.True(result.IsSuccess);
        Assert.Equal(AuthErrorCode.None, result.ErrorCode);
        Assert.NotNull(result.User);
        Assert.Equal("Erick", result.User!.Name);
        Assert.Equal("erick@hearth.app", result.User.Email);

        var savedUser = Assert.Single(_users.Users);
        Assert.Equal(result.User.Id, savedUser.Id);
        Assert.Equal("fake-hash:segredo123", savedUser.PasswordHash);
        Assert.Equal(_clock.UtcNow, savedUser.CreatedAtUtc);

        var defaultAccount = Assert.Single(_accounts.Accounts);
        Assert.Equal(savedUser.Id, defaultAccount.OwnerUserId);
        Assert.Equal(RegisterUserHandler.DefaultAccountName, defaultAccount.Name);
        Assert.Equal(Money.Zero, defaultAccount.InitialBalance);
        Assert.Equal(new DateOnly(2026, 11, 1), defaultAccount.TrackingStartDate);
        Assert.False(defaultAccount.IsInitialBalanceConfirmed);

        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithDuplicateEmailDifferentCase_ReturnsEmailAlreadyInUse()
    {
        var handler = CreateHandler();

        await handler.ExecuteAsync(new RegisterUserCommand("Erick", "erick@hearth.app", "segredo123"));
        var duplicate = await handler.ExecuteAsync(new RegisterUserCommand("Outro", "ERICK@HEARTH.APP", "outrasenha"));

        Assert.False(duplicate.IsSuccess);
        Assert.Equal(AuthErrorCode.EmailAlreadyInUse, duplicate.ErrorCode);
        Assert.Null(duplicate.User);
        Assert.Single(_users.Users);
        Assert.Single(_accounts.Accounts);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WithInvalidName_ReturnsInvalidName(string name)
    {
        var result = await CreateHandler().ExecuteAsync(
            new RegisterUserCommand(name, "erick@hearth.app", "segredo123"));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCode.InvalidName, result.ErrorCode);
        Assert.Empty(_users.Users);
        Assert.Empty(_accounts.Accounts);
    }

    [Fact]
    public async Task ExecuteAsync_WithNameTooLong_ReturnsInvalidName()
    {
        var longName = new string('a', DomainValidation.MaxNameLength + 1);

        var result = await CreateHandler().ExecuteAsync(
            new RegisterUserCommand(longName, "erick@hearth.app", "segredo123"));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCode.InvalidName, result.ErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalido")]
    [InlineData("user@localhost")]
    public async Task ExecuteAsync_WithInvalidEmail_ReturnsInvalidEmail(string email)
    {
        var result = await CreateHandler().ExecuteAsync(
            new RegisterUserCommand("Erick", email, "segredo123"));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCode.InvalidEmail, result.ErrorCode);
        Assert.Empty(_users.Users);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567")]
    public async Task ExecuteAsync_WithPasswordTooShort_ReturnsPasswordTooShort(string password)
    {
        var result = await CreateHandler().ExecuteAsync(
            new RegisterUserCommand("Erick", "erick@hearth.app", password));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCode.PasswordTooShort, result.ErrorCode);
        Assert.Empty(_users.Users);
    }

    [Fact]
    public async Task ExecuteAsync_WithPasswordTooLong_ReturnsPasswordTooLong()
    {
        var password = new string('x', DomainValidation.MaxPasswordLength + 1);

        var result = await CreateHandler().ExecuteAsync(
            new RegisterUserCommand("Erick", "erick@hearth.app", password));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCode.PasswordTooLong, result.ErrorCode);
        Assert.Empty(_users.Users);
    }
}
