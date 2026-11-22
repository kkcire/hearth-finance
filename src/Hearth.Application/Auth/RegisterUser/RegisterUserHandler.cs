using Hearth.Application.Abstractions;
using Hearth.Domain;
using Hearth.Domain.Entities;

namespace Hearth.Application.Auth.RegisterUser;

public sealed class RegisterUserHandler
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RegisterUserHandler(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(users);
        ArgumentNullException.ThrowIfNull(passwordHasher);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        _users = users;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<AuthResult> ExecuteAsync(
        RegisterUserCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Trim().Length > DomainValidation.MaxNameLength)
        {
            return AuthResult.Failure(AuthErrorCode.InvalidName);
        }

        if (!DomainValidation.IsValidEmail(command.Email))
        {
            return AuthResult.Failure(AuthErrorCode.InvalidEmail);
        }

        if (string.IsNullOrEmpty(command.Password) || command.Password.Length < DomainValidation.MinPasswordLength)
        {
            return AuthResult.Failure(AuthErrorCode.PasswordTooShort);
        }

        if (command.Password.Length > DomainValidation.MaxPasswordLength)
        {
            return AuthResult.Failure(AuthErrorCode.PasswordTooLong);
        }

        var normalizedEmail = DomainValidation.NormalizeEmail(command.Email);

        if (await _users.ExistsWithEmailAsync(normalizedEmail, cancellationToken))
        {
            return AuthResult.Failure(AuthErrorCode.EmailAlreadyInUse);
        }

        var passwordHash = _passwordHasher.Hash(command.Password);

        var user = User.Create(
            Guid.CreateVersion7(),
            command.Name,
            normalizedEmail,
            passwordHash,
            _clock.UtcNow);

        await _users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return AuthResult.Success(UserDto.FromEntity(user));
    }
}
