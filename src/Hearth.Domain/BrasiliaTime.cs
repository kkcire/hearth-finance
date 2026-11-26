namespace Hearth.Domain;

/// <summary>
/// Authoritative Brasília timezone (<c>America/Sao_Paulo</c>, UTC-3) helper used to derive
/// an account's immutable tracking start date (Day 1 invariant) from its UTC creation instant.
/// </summary>
public static class BrasiliaTime
{
    public const string IanaTimeZoneId = "America/Sao_Paulo";
    private const string WindowsTimeZoneId = "E. South America Standard Time";

    public static TimeZoneInfo Zone { get; } = ResolveZone();

    /// <summary>
    /// Converts a UTC timestamp to Brasília local time and returns the 1st day of that calendar month.
    /// </summary>
    public static DateOnly DeriveTrackingStartDate(DateTimeOffset createdAtUtc)
    {
        var local = TimeZoneInfo.ConvertTime(createdAtUtc.ToUniversalTime(), Zone);
        return new DateOnly(local.Year, local.Month, 1);
    }

    /// <summary>
    /// Converts a UTC timestamp to the local calendar date in Brasília time.
    /// </summary>
    public static DateOnly ToLocalDate(DateTimeOffset utcInstant)
    {
        var local = TimeZoneInfo.ConvertTime(utcInstant.ToUniversalTime(), Zone);
        return DateOnly.FromDateTime(local.DateTime);
    }

    private static TimeZoneInfo ResolveZone()
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById(IanaTimeZoneId, out var iana))
        {
            return iana;
        }

        if (TimeZoneInfo.TryFindSystemTimeZoneById(WindowsTimeZoneId, out var windows))
        {
            return windows;
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            IanaTimeZoneId,
            TimeSpan.FromHours(-3),
            "Brasília Standard Time",
            "Brasília Standard Time");
    }
}
