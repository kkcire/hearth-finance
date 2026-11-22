using Hearth.Application.Abstractions;
using Hearth.Domain;

namespace Hearth.Application.Auth.AuthenticateUser;

public sealed class AuthenticateUserHandler
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public AuthenticateUserHandler(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(users);
        ArgumentNullException.ThrowIfNull(passwordHasher);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _users = users;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<AuthResult> ExecuteAsync(
        AuthenticateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!DomainValidation.IsValidEmail(command.Email)
            || string.IsNullOrEmpty(command.Password)
            || command.Password.Length > DomainValidation.MaxPasswordLength)
        {
            return AuthResult.Failure(AuthErrorCode.InvalidCredentials);
        }

        var normalizedEmail = DomainValidation.NormalizeEmail(command.Email);
        var user = await _users.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user is null)
        {
            return AuthResult.Failure(AuthErrorCode.InvalidCredentials);
        }

        var outcome = _passwordHasher.Verify(user.PasswordHash, command.Password);

        if (outcome == PasswordVerificationOutcome.Failed)
        {
            return AuthResult.Failure(AuthErrorCode.InvalidCredentials);
        }

        if (outcome == PasswordVerificationOutcome.SuccessRehashNeeded)
        {
            user.UpdatePasswordHash(_passwordHasher.Hash(command.Password));
            await _users.UpdateAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return AuthResult.Success(UserDto.FromEntity(user));
    }
}
