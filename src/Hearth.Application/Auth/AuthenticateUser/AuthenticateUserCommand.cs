namespace Hearth.Application.Auth.AuthenticateUser;

public sealed record AuthenticateUserCommand(string Email, string Password);
