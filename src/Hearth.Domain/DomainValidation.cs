using Hearth.Domain.ValueObjects;

namespace Hearth.Domain;

public static class DomainValidation
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 200;
    public const int MaxEmailLength = 254;
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;

    private const int MaxEmailLocalLength = 64;

    public static bool IsValidEmail(string? value) =>
        !string.IsNullOrWhiteSpace(value) && IsValidTrimmedEmail(value.Trim());

    public static string NormalizeEmail(string value) =>
        ValidateAndNormalizeEmail(value, nameof(value));

    internal static string ValidateAndNormalizeEmail(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);

        var trimmed = value.Trim();

        if (!IsValidTrimmedEmail(trimmed))
        {
            throw new ArgumentException("The email address is not valid.", paramName);
        }

        return trimmed.ToLowerInvariant();
    }

    internal static void EnsureNotEmpty(Guid id, string paramName, string label = "id")
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException($"The {label} must not be empty.", paramName);
        }
    }

    internal static void EnsureOptionalNotEmpty(Guid? id, string paramName, string label = "id")
    {
        if (id.HasValue)
        {
            EnsureNotEmpty(id.Value, paramName, label);
        }
    }

    internal static void EnsurePositiveAmount(Money amount, string paramName, string label = "amount")
    {
        if (amount <= Money.Zero)
        {
            throw new ArgumentOutOfRangeException(
                paramName, amount, $"The {label} must be greater than zero.");
        }
    }

    internal static string ValidateRequiredText(string value, string paramName, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException(
                $"The text must not exceed {maxLength} characters.", paramName);
        }

        return trimmed;
    }

    internal static string? ValidateOptionalText(string? value, string paramName, int maxLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : ValidateRequiredText(value, paramName, maxLength);

    private static bool IsValidTrimmedEmail(string trimmed)
    {
        if (trimmed.Length > MaxEmailLength || ContainsForbiddenEmailChar(trimmed))
        {
            return false;
        }

        var atIndex = trimmed.IndexOf('@');
        if (atIndex <= 0 || atIndex != trimmed.LastIndexOf('@'))
        {
            return false;
        }

        var local = trimmed[..atIndex];
        var domain = trimmed[(atIndex + 1)..];

        return local.Length <= MaxEmailLocalLength && IsValidEmailDomain(domain);
    }

    private static bool ContainsForbiddenEmailChar(string value)
    {
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsValidEmailDomain(string domain) =>
        domain.Length >= 3
        && domain.IndexOf('.') > 0
        && !domain.EndsWith('.')
        && !domain.Contains("..", StringComparison.Ordinal);
}
