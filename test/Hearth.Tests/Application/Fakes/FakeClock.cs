using Hearth.Application.Abstractions;

namespace Hearth.Tests.Application.Fakes;

internal sealed class FakeClock : IClock
{
    public FakeClock(DateOnly today, DateTimeOffset? utcNow = null)
    {
        Today = today;
        UtcNow = utcNow ?? new DateTimeOffset(today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
    }

    public DateOnly Today { get; set; }

    public DateTimeOffset UtcNow { get; set; }
}
