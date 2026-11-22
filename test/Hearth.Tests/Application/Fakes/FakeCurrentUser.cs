using Hearth.Application.Abstractions;

namespace Hearth.Tests.Application.Fakes;

internal sealed class FakeCurrentUser : ICurrentUser
{
    public static readonly Guid DefaultUserId = Guid.Parse("01900000-0000-7000-8000-000000000001");

    public FakeCurrentUser(
        Guid? userId = null,
        string? name = "Erick",
        string? email = "erick@hearth.app",
        bool isAuthenticated = true)
    {
        IsAuthenticated = isAuthenticated;
        UserId = isAuthenticated ? (userId ?? DefaultUserId) : null;
        Name = isAuthenticated ? name : null;
        Email = isAuthenticated ? email : null;
    }

    public bool IsAuthenticated { get; set; }

    public Guid? UserId { get; set; }

    public string? Name { get; set; }

    public string? Email { get; set; }

    public static FakeCurrentUser Anonymous() => new(isAuthenticated: false);
}
