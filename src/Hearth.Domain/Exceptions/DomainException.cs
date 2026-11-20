namespace Hearth.Domain.Exceptions;

/// <summary>
/// Exception thrown when a domain business rule is violated.
/// Distinct from argument validation errors (<see cref="ArgumentException"/>) and unexpected bugs.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }
}
