using Hearth.Domain.ValueObjects;

namespace Hearth.Domain;

internal static class DomainValidation
{
    public static void EnsureNotEmpty(Guid id, string paramName, string label = "id")
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException($"The {label} must not be empty.", paramName);
        }
    }

    public static void EnsurePositiveAmount(Money amount, string paramName, string label = "amount")
    {
        if (amount <= Money.Zero)
        {
            throw new ArgumentOutOfRangeException(
                paramName, amount, $"The {label} must be greater than zero.");
        }
    }

    public static string ValidateRequiredText(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);

        return value.Trim();
    }

    public static void EnsureDefinedCategory(ExpenseCategory category, string paramName)
    {
        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(
                paramName, category, "The category is not a known category.");
        }
    }
}
