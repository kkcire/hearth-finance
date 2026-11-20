namespace Hearth.Application.Exceptions;

/// <summary>
/// Exception thrown when a requested resource does not exist.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string resourceName, Guid id)
        : base($"{resourceName} '{id}' was not found.")
    {
        ResourceName = resourceName;
        ResourceId = id;
    }

    public string ResourceName { get; }

    public Guid ResourceId { get; }
}
