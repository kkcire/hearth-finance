namespace Hearth.Application.Auth;

public enum AuthErrorCode
{
    None = 0,
    InvalidName = 1,
    InvalidEmail = 2,
    PasswordTooShort = 3,
    PasswordTooLong = 4,
    EmailAlreadyInUse = 5,
    InvalidCredentials = 6,
    RegistrationDisabled = 7,
}
