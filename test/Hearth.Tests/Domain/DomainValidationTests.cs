using Hearth.Domain;
using Xunit;

namespace Hearth.Tests.Domain;

public sealed class DomainValidationTests
{
    [Fact]
    public void IsValidEmail_EnforcesLocalLengthControlCharsAndMaxEmailLength()
    {
        var local64 = new string('a', 64);
        var local65 = new string('a', 65);
        var maxDomain = $"{new string('b', 185)}.com"; // 64 + 1 + 189 = 254

        Assert.True(DomainValidation.IsValidEmail($"  {local64}@{maxDomain}  "));
        Assert.False(DomainValidation.IsValidEmail($"{local64}@{new string('b', 186)}.com")); // 255
        Assert.False(DomainValidation.IsValidEmail($"{local65}@example.com"));
        Assert.False(DomainValidation.IsValidEmail("user\0@example.com"));
        Assert.False(DomainValidation.IsValidEmail("user\n@example.com"));
        Assert.False(DomainValidation.IsValidEmail("user\t@example.com"));
        Assert.False(DomainValidation.IsValidEmail("user name@example.com"));
        Assert.False(DomainValidation.IsValidEmail("user@domain..com"));
        Assert.False(DomainValidation.IsValidEmail("user@.domain.com"));
        Assert.False(DomainValidation.IsValidEmail("user@domain.com."));
    }

    [Fact]
    public void NormalizeEmail_TrimsLowercasesAndRejectsInvalid()
    {
        Assert.Equal("erick@hearth.app", DomainValidation.NormalizeEmail("  ERICK@Hearth.APP  "));
        Assert.ThrowsAny<ArgumentException>(() => DomainValidation.NormalizeEmail("   "));
        Assert.Throws<ArgumentException>(() => DomainValidation.NormalizeEmail("invalido"));
    }
}
