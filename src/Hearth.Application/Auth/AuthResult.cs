namespace Hearth.Application.Auth;

public sealed record AuthResult(bool IsSuccess, UserDto? User, AuthErrorCode ErrorCode)
{
    public static AuthResult Success(UserDto user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new AuthResult(true, user, AuthErrorCode.None);
    }

    public static AuthResult Failure(AuthErrorCode errorCode)
    {
        if (errorCode == AuthErrorCode.None)
        {
            throw new ArgumentException("Failure error code must not be None.", nameof(errorCode));
        }

        return new AuthResult(false, null, errorCode);
    }
}
