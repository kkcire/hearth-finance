using Hearth.Application.Abstractions;
using Hearth.Infrastructure.Security;
using Xunit;

namespace Hearth.Tests.Infrastructure.Security;

public sealed class IdentityPasswordHasherTests
{
    private readonly IdentityPasswordHasher _hasher = new();

    [Fact]
    public void Hash_SamePasswordTwice_GeneratesDistinctSaltedHashesThatBothVerify()
    {
        var hash1 = _hasher.Hash("minhasenha123");
        var hash2 = _hasher.Hash("minhasenha123");

        Assert.NotEqual("minhasenha123", hash1);
        Assert.NotEqual(hash1, hash2);
        Assert.Equal(PasswordVerificationOutcome.Success, _hasher.Verify(hash1, "minhasenha123"));
        Assert.Equal(PasswordVerificationOutcome.Success, _hasher.Verify(hash2, "minhasenha123"));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFailed()
    {
        var hash = _hasher.Hash("minhasenha123");

        Assert.Equal(PasswordVerificationOutcome.Failed, _hasher.Verify(hash, "outrasenha123"));
    }

    [Fact]
    public void Verify_MalformedHash_ReturnsFailedWithoutThrowing()
    {
        Assert.Equal(PasswordVerificationOutcome.Failed, _hasher.Verify("not-valid-base64!!!", "minhasenha123"));
        Assert.Equal(PasswordVerificationOutcome.Failed, _hasher.Verify("", "minhasenha123"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Hash_NullOrEmpty_Throws(string? password)
    {
        Assert.ThrowsAny<ArgumentException>(() => _hasher.Hash(password!));
    }
}
