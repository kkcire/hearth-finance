using Hearth.Application.Auth;
using Hearth.Application.Auth.AuthenticateUser;
using Hearth.Domain.Entities;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.Auth;

public sealed class AuthenticateUserHandlerTests
{
    private readonly InMemoryUserRepository _users = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private AuthenticateUserHandler CreateHandler() =>
        new(_users, _hasher, _unitOfWork);

    private async Task<User> SeedUserAsync(
        string email = "erick@hearth.app",
        string passwordHash = "fake-hash:segredo123")
    {
        var user = User.Create(
            Guid.NewGuid(),
            "Erick",
            email,
            passwordHash,
            new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero));

        await _users.AddAsync(user);
        return user;
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCredentialsAndMixedCaseEmail_ReturnsUser()
    {
        var seeded = await SeedUserAsync();

        var result = await CreateHandler().ExecuteAsync(
            new AuthenticateUserCommand("  ERICK@Hearth.App ", "segredo123"));

        Assert.True(result.IsSuccess);
        Assert.Equal(AuthErrorCode.None, result.ErrorCode);
        Assert.NotNull(result.User);
        Assert.Equal(seeded.Id, result.User!.Id);
        Assert.Equal("Erick", result.User.Name);
        Assert.Equal("erick@hearth.app", result.User.Email);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithWrongPassword_ReturnsInvalidCredentials()
    {
        await SeedUserAsync();

        var result = await CreateHandler().ExecuteAsync(
            new AuthenticateUserCommand("erick@hearth.app", "errada123"));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCode.InvalidCredentials, result.ErrorCode);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownEmail_ReturnsSameInvalidCredentialsError()
    {
        await SeedUserAsync();

        var result = await CreateHandler().ExecuteAsync(
            new AuthenticateUserCommand("naoexiste@hearth.app", "segredo123"));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCode.InvalidCredentials, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRehashNeeded_UpdatesStoredHashAndSaves()
    {
        var seeded = await SeedUserAsync(passwordHash: "legacy-hash:segredo123");

        var result = await CreateHandler().ExecuteAsync(
            new AuthenticateUserCommand("erick@hearth.app", "segredo123"));

        Assert.True(result.IsSuccess);
        Assert.Equal("fake-hash:segredo123", seeded.PasswordHash);
        Assert.Equal(1, _users.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }
}
