using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Hearth.Infrastructure.Security;

public sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        return _hasher.HashPassword(null!, password);
    }

    public PasswordVerificationOutcome Verify(string passwordHash, string providedPassword)
    {
        if (string.IsNullOrWhiteSpace(passwordHash) || string.IsNullOrEmpty(providedPassword))
        {
            return PasswordVerificationOutcome.Failed;
        }

        try
        {
            var result = _hasher.VerifyHashedPassword(null!, passwordHash, providedPassword);

            return result switch
            {
                PasswordVerificationResult.Success => PasswordVerificationOutcome.Success,
                PasswordVerificationResult.SuccessRehashNeeded => PasswordVerificationOutcome.SuccessRehashNeeded,
                _ => PasswordVerificationOutcome.Failed,
            };
        }
        catch (FormatException)
        {
            return PasswordVerificationOutcome.Failed;
        }
    }
}
