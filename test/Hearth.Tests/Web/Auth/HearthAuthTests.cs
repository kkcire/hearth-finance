using System.Security.Claims;
using Hearth.Application.Auth;
using Hearth.Web.Auth;
using Xunit;

namespace Hearth.Tests.Web.Auth;

public sealed class HearthAuthTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/cartao/faturas")]
    [InlineData("/cartao/faturas?mes=2026-11")]
    [InlineData("/onboarding")]
    public void IsSafeLocalReturnUrl_ValidLocalUrls_ReturnsTrue(string returnUrl)
    {
        Assert.True(HearthAuth.IsSafeLocalReturnUrl(returnUrl));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://evil.com")]
    [InlineData("http://evil.com/login")]
    [InlineData("//evil.com")]
    [InlineData("/\\evil.com")]
    [InlineData("\\evil")]
    [InlineData("/login")]
    [InlineData("/login?returnUrl=/")]
    [InlineData("/cadastro")]
    [InlineData("/auth/logout")]
    [InlineData("/path\r\nHeader: injected")]
    public void IsSafeLocalReturnUrl_UnsafeOrAuthLoopUrls_ReturnsFalse(string? returnUrl)
    {
        Assert.False(HearthAuth.IsSafeLocalReturnUrl(returnUrl));
    }

    [Fact]
    public void CreatePrincipalAndTryReadUser_RoundTripsUserIdentity()
    {
        var user = new UserDto(Guid.CreateVersion7(), "Erick", "erick@hearth.app");

        var principal = HearthAuth.CreatePrincipal(user);

        var ok = HearthAuth.TryReadUser(principal, out var userId, out var name, out var email);

        Assert.True(ok);
        Assert.Equal(user.Id, userId);
        Assert.Equal("Erick", name);
        Assert.Equal("erick@hearth.app", email);
    }

    [Fact]
    public void TryReadUser_AnonymousPrincipal_ReturnsFalse()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.False(HearthAuth.TryReadUser(anonymous, out _, out _, out _));
    }

    [Fact]
    public void ToPortugueseError_MapsEveryAuthErrorCode()
    {
        foreach (var code in Enum.GetValues<AuthErrorCode>())
        {
            if (code == AuthErrorCode.None)
            {
                continue;
            }

            var message = HearthAuth.ToPortugueseError(code);
            Assert.False(string.IsNullOrWhiteSpace(message));
        }
    }
}
