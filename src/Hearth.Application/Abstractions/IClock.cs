namespace Hearth.Application.Abstractions;

/// <summary>
/// Provides the current calendar date and UTC timestamp for time-dependent business rules
/// such as fixed expense freezing, credit card invoice locking, and entity creation timestamps.
/// </summary>
public interface IClock
{
    DateOnly Today { get; }

    DateTimeOffset UtcNow { get; }
}
