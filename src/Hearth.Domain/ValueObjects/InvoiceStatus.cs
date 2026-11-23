namespace Hearth.Domain.ValueObjects;

/// <summary>
/// Derived lifecycle status of a credit card invoice cycle on a given local date.
/// </summary>
public enum InvoiceStatus
{
    Open = 0,
    Closed = 1,
    Paid = 2,
}
