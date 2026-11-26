namespace Hearth.Tests;

internal static class TestTime
{
    /// <summary>Returns noon BRT (15:00 UTC) on the given calendar date for deterministic account creation timestamps in tests.</summary>
    public static DateTimeOffset UtcOn(DateOnly date) =>
        new(date.ToDateTime(new TimeOnly(15, 0)), TimeSpan.Zero);

    public static DateTimeOffset UtcOn(int year, int month, int day) =>
        UtcOn(new DateOnly(year, month, day));
}
