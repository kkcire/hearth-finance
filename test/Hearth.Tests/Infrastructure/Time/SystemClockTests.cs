using Hearth.Infrastructure.Time;
using Xunit;

namespace Hearth.Tests.Infrastructure.Time;

public class SystemClockTests
{
    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    [Fact]
    public void Today_LateEveningInBrazilWhenUtcIsAlreadyNextDay_ReturnsBrazilCalendarDate()
    {
        // 2026-11-21 01:30 UTC is 2026-11-20 22:30 in America/Sao_Paulo (UTC-3).
        var provider = new FixedTimeProvider(new DateTimeOffset(2026, 11, 21, 1, 30, 0, TimeSpan.Zero));
        var clock = new SystemClock(provider);

        Assert.Equal(new DateOnly(2026, 11, 20), clock.Today);
    }

    [Fact]
    public void Today_ExactMidnightInBrazil_ReturnsNewCalendarDate()
    {
        // 2026-11-21 03:00 UTC is 2026-11-21 00:00 in America/Sao_Paulo (UTC-3).
        var provider = new FixedTimeProvider(new DateTimeOffset(2026, 11, 21, 3, 0, 0, TimeSpan.Zero));
        var clock = new SystemClock(provider);

        Assert.Equal(new DateOnly(2026, 11, 21), clock.Today);
    }

    [Fact]
    public void Today_CustomTimeZone_ConvertsFromUtc()
    {
        // 2026-11-20 20:00 UTC is 2026-11-21 05:00 in Asia/Tokyo (UTC+9).
        var provider = new FixedTimeProvider(new DateTimeOffset(2026, 11, 20, 20, 0, 0, TimeSpan.Zero));
        var clock = new SystemClock(provider, "Asia/Tokyo");

        Assert.Equal(new DateOnly(2026, 11, 21), clock.Today);
    }

    [Fact]
    public void UtcNow_ReturnsProviderUtcTimestamp()
    {
        var expected = new DateTimeOffset(2026, 11, 22, 14, 45, 0, TimeSpan.Zero);
        var provider = new FixedTimeProvider(expected);
        var clock = new SystemClock(provider);

        Assert.Equal(expected, clock.UtcNow);
    }

    [Fact]
    public void Constructor_InvalidArguments_Throw()
    {
        var provider = new FixedTimeProvider(DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentNullException>(() => new SystemClock(null!));
        Assert.ThrowsAny<ArgumentException>(() => new SystemClock(provider, ""));
        Assert.Throws<TimeZoneNotFoundException>(() => new SystemClock(provider, "Invalid/Zone"));
    }
}
