using Hearth.Domain;
using Hearth.Domain.Entities;
using Xunit;

namespace Hearth.Tests.Domain.Entities;

public class UserTests
{
    private static readonly DateTimeOffset DefaultCreatedAt =
        new(2026, 11, 22, 14, 0, 0, TimeSpan.Zero);

    private static User NewUser(
        Guid? id = null,
        string name = "Erick",
        string email = "erick@hearth.app",
        string passwordHash = "v1:hashed-password",
        DateTimeOffset? createdAtUtc = null) =>
        User.Create(
            id ?? Guid.NewGuid(),
            name,
            email,
            passwordHash,
            createdAtUtc ?? DefaultCreatedAt);

    [Fact]
    public void Create_StoresGivenValues_TrimsName_NormalizesEmailAndUtcTimestamp()
    {
        var id = Guid.NewGuid();
        var brTime = new DateTimeOffset(2026, 11, 22, 11, 30, 0, TimeSpan.FromHours(-3));

        var user = User.Create(id, "  Erick  ", "  Erick.Silva@Hearth.APP  ", "hash-123", brTime);

        Assert.Equal(id, user.Id);
        Assert.Equal("Erick", user.Name);
        Assert.Equal("erick.silva@hearth.app", user.Email);
        Assert.Equal("hash-123", user.PasswordHash);
        Assert.Equal(TimeSpan.Zero, user.CreatedAtUtc.Offset);
        Assert.Equal(new DateTimeOffset(2026, 11, 22, 14, 30, 0, TimeSpan.Zero), user.CreatedAtUtc);
    }

    [Fact]
    public void Create_AcceptsSingleWordName()
    {
        var user = NewUser(name: "Ana");

        Assert.Equal("Ana", user.Name);
    }

    [Fact]
    public void Create_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() => NewUser(id: Guid.Empty));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_NameIsRequired(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => NewUser(name: name!));
    }

    [Fact]
    public void Create_NameExceedingMaxLength_Throws()
    {
        var tooLong = new string('a', DomainValidation.MaxNameLength + 1);

        Assert.Throws<ArgumentException>(() => NewUser(name: tooLong));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("@domain.com")]
    [InlineData("user@")]
    [InlineData("user@localhost")]
    [InlineData("user@.domain.com")]
    [InlineData("user@domain.com.")]
    [InlineData("user@domain..com")]
    [InlineData("user name@domain.com")]
    [InlineData("a@b@domain.com")]
    public void Create_InvalidEmail_Throws(string? email)
    {
        Assert.ThrowsAny<ArgumentException>(() => NewUser(email: email!));
    }

    [Fact]
    public void Create_EmailExceedingMaxLength_Throws()
    {
        var local = new string('a', 60);
        var domain = new string('b', DomainValidation.MaxEmailLength - 60) + ".com";
        var tooLong = $"{local}@{domain}";

        Assert.Throws<ArgumentException>(() => NewUser(email: tooLong));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_PasswordHashIsRequired(string? passwordHash)
    {
        Assert.ThrowsAny<ArgumentException>(() => NewUser(passwordHash: passwordHash!));
    }

    [Fact]
    public void Create_DefaultCreatedAtUtc_Throws()
    {
        Assert.Throws<ArgumentException>(() => NewUser(createdAtUtc: default(DateTimeOffset)));
    }

    [Fact]
    public void UpdatePasswordHash_UpdatesHash()
    {
        var user = NewUser(passwordHash: "old-hash");

        user.UpdatePasswordHash("new-hash");

        Assert.Equal("new-hash", user.PasswordHash);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdatePasswordHash_Invalid_ThrowsAndKeepsOriginal(string? newHash)
    {
        var user = NewUser(passwordHash: "old-hash");

        Assert.ThrowsAny<ArgumentException>(() => user.UpdatePasswordHash(newHash!));

        Assert.Equal("old-hash", user.PasswordHash);
    }
}
