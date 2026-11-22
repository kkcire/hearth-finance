using Hearth.Domain.ValueObjects;

namespace Hearth.Domain;

public static class DomainValidation
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 200;
    public const int MaxEmailLength = 254;
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;

    public static bool IsValidEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > MaxEmailLength)
        {
            return false;
        }

        foreach (var c in trimmed)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                return false;
            }
        }

        var atIndex = trimmed.IndexOf('@');
        if (atIndex <= 0 || atIndex != trimmed.LastIndexOf('@'))
        {
            return false;
        }

        var local = trimmed[..atIndex];
        var domain = trimmed[(atIndex + 1)..];

        if (local.Length > 64 || domain.Length < 3)
        {
            return false;
        }

        var dotIndex = domain.IndexOf('.');
        if (dotIndex <= 0 || domain.EndsWith('.') || domain.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    public static string NormalizeEmail(string value)
    {
        return ValidateAndNormalizeEmail(value, nameof(value));
    }

    internal static string ValidateAndNormalizeEmail(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);

        var trimmed = value.Trim();

        if (!IsValidEmail(trimmed))
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
}
