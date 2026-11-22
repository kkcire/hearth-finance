using System.Security.Claims;
using Hearth.Application.Auth;
using Hearth.Tests.Application.Fakes;
using Hearth.Web.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace Hearth.Tests.Web;

internal sealed class TestAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly FakeCurrentUser _currentUser;

    public TestAuthenticationStateProvider(FakeCurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
        {
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
        }

        var principal = HearthAuth.CreatePrincipal(new UserDto(
            userId,
            _currentUser.Name ?? "Erick",
            _currentUser.Email ?? "erick@hearth.app"));

        return Task.FromResult(new AuthenticationState(principal));
    }

    public void NotifyChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
