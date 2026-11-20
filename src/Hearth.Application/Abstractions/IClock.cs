namespace Hearth.Application.Abstractions;

/// <summary>
/// Provides the current calendar date for time-dependent business rules
/// such as fixed expense freezing and credit card invoice locking.
/// </summary>
public interface IClock
{
    DateOnly Today { get; }
}
