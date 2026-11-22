using Hearth.Application.Abstractions;

namespace Hearth.Tests.Application.Fakes;

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public bool RequireRehashOnNextVerify { get; set; }

    public string HashPrefix { get; set; } = "fake-hash:";

    public string Hash(string password) => $"{HashPrefix}{password}";

    public PasswordVerificationOutcome Verify(string passwordHash, string providedPassword)
    {
        var matches = passwordHash == $"{HashPrefix}{providedPassword}"
                      || passwordHash == $"legacy-hash:{providedPassword}";

        if (!matches)
        {
            return PasswordVerificationOutcome.Failed;
        }

        if (RequireRehashOnNextVerify || passwordHash.StartsWith("legacy-hash:", StringComparison.Ordinal))
        {
            RequireRehashOnNextVerify = false;
            return PasswordVerificationOutcome.SuccessRehashNeeded;
        }

        return PasswordVerificationOutcome.Success;
    }
}
