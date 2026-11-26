namespace Hearth.Domain.ValueObjects;

/// <summary>
/// Semantic classification of an isolated monthly result relative to the break-even point.
/// </summary>
public enum BreakEvenStatus
{
    /// <summary>Result sits clearly above the break-even attention zone (positive margin).</summary>
    Healthy = 0,

    /// <summary>Result is at zero or within the tight attention band near break-even.</summary>
    BreakEvenWarning = 1,

    /// <summary>Result is below break-even (negative monthly balance / deficit).</summary>
    Deficit = 2,

    /// <summary>Month has zero income and zero expenses (no financial activity).</summary>
    NoActivity = 3,
}
