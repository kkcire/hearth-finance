using Hearth.Application.Abstractions;
using Hearth.Domain;

namespace Hearth.Tests.Application.Fakes;

internal sealed class FakeClock : IClock
{
    private DateOnly _today;
    private DateTimeOffset _utcNow;

    public FakeClock(DateOnly today, DateTimeOffset? utcNow = null)
    {
        _today = today;
        _utcNow = utcNow ?? new DateTimeOffset(today.ToDateTime(new TimeOnly(15, 0)), TimeSpan.Zero);
    }

    public DateOnly Today
    {
        get => _today;
        set
        {
            _today = value;
            _utcNow = new DateTimeOffset(value.ToDateTime(new TimeOnly(15, 0)), TimeSpan.Zero);
        }
    }

    public DateTimeOffset UtcNow
    {
        get => _utcNow;
        set
        {
            _utcNow = value.ToUniversalTime();
            _today = BrasiliaTime.ToLocalDate(_utcNow);
        }
    }
}
