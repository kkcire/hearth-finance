namespace Hearth.Web.Auth;

public sealed class HearthAuthOptions
{
    public const string SectionName = "Hearth";

    public bool AllowRegistration { get; set; }

    public AuthRateLimitOptions AuthRateLimit { get; set; } = new();
}

public sealed class AuthRateLimitOptions
{
    public bool Enabled { get; set; } = true;

    public int PermitLimit { get; set; } = 10;

    public int WindowSeconds { get; set; } = 60;
}
