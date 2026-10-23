namespace Hearth.Domain.ValueObjects;

/// <summary>
/// An amount of money in BRL, with at most two decimal places.
/// Immutable and compared by value. <c>default(Money)</c> is R$ 0.00.
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    private const int Scale = 2;
    private const decimal CentsPerUnit = 100m;

    private Money(decimal amount)
    {
        Amount = amount;
    }

    public static Money Zero => default;

    public decimal Amount { get; }

    /// <summary>
    /// Creates a <see cref="Money"/> from a decimal. Rejects values with more than
    /// two decimal places instead of rounding them silently.
    /// </summary>
    public static Money FromDecimal(decimal amount)
    {
        if (decimal.Round(amount, Scale) != amount)
        {
            throw new ArgumentException(
                $"Monetary amounts support at most {Scale} decimal places.",
                nameof(amount));
        }

        return new Money(amount);
    }

    public static Money Sum(IEnumerable<Money> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var total = Zero;
        foreach (var value in values)
        {
            total += value;
        }

        return total;
    }

    /// <summary>
    /// Splits this amount into <paramref name="parts"/> installments. Every installment
    /// has the same value except the last one, which absorbs the remaining cents, so the
    /// sum of the installments is exactly equal to the original amount.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The number of parts is not positive.</exception>
    /// <exception cref="InvalidOperationException">
    /// The amount is not positive, or it is too small to give each installment at least R$ 0.01.
    /// </exception>
    public IReadOnlyList<Money> Split(int parts)
    {
        if (parts <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(parts), parts, "The number of parts must be greater than zero.");
        }

        if (Amount <= 0)
        {
            throw new InvalidOperationException("Only positive amounts can be split.");
        }

        var totalCents = decimal.ToInt64(Amount * CentsPerUnit);
        var baseCents = totalCents / parts;

        if (baseCents < 1)
        {
            throw new InvalidOperationException(
                "The amount is too small to give each part at least R$ 0.01.");
        }

        var remainderCents = totalCents - (baseCents * parts);

        var result = new Money[parts];
        for (var i = 0; i < parts - 1; i++)
        {
            result[i] = FromCents(baseCents);
        }

        result[parts - 1] = FromCents(baseCents + remainderCents);

        return result;
    }

    public Money Negate() => new(-Amount);

    public int CompareTo(Money other) => Amount.CompareTo(other.Amount);

    public static Money operator +(Money left, Money right) => new(left.Amount + right.Amount);

    public static Money operator -(Money left, Money right) => new(left.Amount - right.Amount);

    public static Money operator -(Money value) => value.Negate();

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    private static Money FromCents(long cents) => new(cents / CentsPerUnit);
}