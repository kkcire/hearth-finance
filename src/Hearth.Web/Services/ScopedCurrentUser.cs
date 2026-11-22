using Hearth.Application.Abstractions;

namespace Hearth.Web.Services;

/// <summary>
/// Scoped <see cref="ICurrentUser"/> holder populated per short-lived execution scope
/// (from the Blazor Server circuit's authentication state) or from an HTTP request.
/// </summary>
public sealed class ScopedCurrentUser : ICurrentUser
{
    public bool IsAuthenticated { get; private set; }

    public Guid? UserId { get; private set; }

    public string? Name { get; private set; }

    public string? Email { get; private set; }

    public void SetAuthenticated(Guid userId, string? name, string? email)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("The user id must not be empty.", nameof(userId));
        }

        IsAuthenticated = true;
        UserId = userId;
        Name = name;
        Email = email;
    }

    public void Clear()
    {
        IsAuthenticated = false;
        UserId = null;
        Name = null;
        Email = null;
    }
}
