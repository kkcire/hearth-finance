using Hearth.Domain.ValueObjects;

namespace Hearth.Domain;

public static class DomainValidation
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 200;

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
