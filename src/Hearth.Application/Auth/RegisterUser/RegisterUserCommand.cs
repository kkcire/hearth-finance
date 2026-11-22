namespace Hearth.Application.Auth.RegisterUser;

public sealed record RegisterUserCommand(string Name, string Email, string Password);
