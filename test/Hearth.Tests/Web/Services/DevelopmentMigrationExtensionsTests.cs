using Hearth.Web.Services;

namespace Hearth.Tests.Web.Services;

public sealed class DevelopmentMigrationExtensionsTests
{
    [Theory]
    [InlineData("localhost")]
    [InlineData("LOCALHOST")]
    [InlineData(" 127.0.0.1 ")]
    [InlineData("::1")]
    public void IsLocalHost_LocalAddresses_ReturnsTrue(string host)
    {
        Assert.True(DevelopmentMigrationExtensions.IsLocalHost(host));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ep-cool-pine.us-east-2.aws.neon.tech")]
    [InlineData("db.project.supabase.co")]
    [InlineData("10.0.0.5")]
    public void IsLocalHost_RemoteOrEmptyAddresses_ReturnsFalse(string? host)
    {
        Assert.False(DevelopmentMigrationExtensions.IsLocalHost(host));
    }
}
