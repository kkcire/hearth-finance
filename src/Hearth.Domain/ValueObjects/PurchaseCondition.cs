namespace Hearth.Domain.ValueObjects;

public enum PurchaseConditionKind
{
    Own = 1,
    Others = 2,
    Custom = 3,
}

/// <summary>
/// A label that says whose purchase it is, used only for filtering.
/// It never changes any calculation and never creates amounts to receive.
/// </summary>
public sealed record PurchaseCondition
{
    private PurchaseCondition(PurchaseConditionKind kind, string? name)
    {
        Kind = kind;
        Name = name;
    }

    /// <summary>The user's own purchase, with no additional condition.</summary>
    public static PurchaseCondition Own { get; } = new(PurchaseConditionKind.Own, null);

    /// <summary>A purchase that belongs to someone else.</summary>
    public static PurchaseCondition Others { get; } = new(PurchaseConditionKind.Others, null);

    public PurchaseConditionKind Kind { get; }

    /// <summary>The user-defined name. Only present when <see cref="Kind"/> is Custom.</summary>
    public string? Name { get; }

    /// <summary>A purchase identified by a user-defined name, which is required.</summary>
    /// <exception cref="ArgumentException">The name is null, empty or whitespace.</exception>
    public static PurchaseCondition Custom(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new PurchaseCondition(PurchaseConditionKind.Custom, name.Trim());
    }
}