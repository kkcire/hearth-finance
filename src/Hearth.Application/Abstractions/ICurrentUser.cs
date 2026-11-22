namespace Hearth.Application.Abstractions;

/// <summary>
/// Exposes the authenticated user for the current application execution scope.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    string? Name { get; }

    string? Email { get; }

    Guid GetRequiredUserId() =>
        IsAuthenticated && UserId is { } userId && userId != Guid.Empty
            ? userId
            : throw new UnauthorizedAccessException("User is not authenticated.");
}
