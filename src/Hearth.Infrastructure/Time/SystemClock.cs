using Hearth.Application.Abstractions;

namespace Hearth.Infrastructure.Time;

/// <summary>
/// Provides the current calendar date in a configured time zone (defaulting to
/// <c>America/Sao_Paulo</c>), so date-sensitive business rules behave consistently
/// even when hosted on UTC servers.
/// </summary>
public sealed class SystemClock : IClock
{
    public const string DefaultTimeZoneId = "America/Sao_Paulo";

    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _timeZone;

    public SystemClock(TimeProvider timeProvider, string timeZoneId = DefaultTimeZoneId)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        _timeProvider = timeProvider;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }

    public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    public DateOnly Today
    {
        get
        {
            var localDateTime = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), _timeZone);
            return DateOnly.FromDateTime(localDateTime.DateTime);
        }
    }
}
