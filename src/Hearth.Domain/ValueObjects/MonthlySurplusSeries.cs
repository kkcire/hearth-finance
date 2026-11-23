namespace Hearth.Domain.ValueObjects;

/// <summary>
/// Monthly surplus cascade for the selected month together with the chronological evolution
/// series and the average net monthly cash variation across the series.
/// Created only by <c>MonthlySurplusCalculator</c>.
/// </summary>
public sealed record MonthlySurplusSeries
{
    internal MonthlySurplusSeries(
        DateOnly asOfDate,
        MonthlySurplus selectedMonth,
        IReadOnlyList<MonthlySurplus> points,
        Money averageMonthlyVariation)
    {
        AsOfDate = asOfDate;
        SelectedMonth = selectedMonth;
        Points = points;
        AverageMonthlyVariation = averageMonthlyVariation;
    }

    /// <summary>The reference date (<c>today</c>) used as the cash cutoff.</summary>
    public DateOnly AsOfDate { get; }

    /// <summary>Detailed monthly surplus cascade for the requested month.</summary>
    public MonthlySurplus SelectedMonth { get; }

    /// <summary>Chronological series of historical, current, and future monthly surpluses.</summary>
    public IReadOnlyList<MonthlySurplus> Points { get; }

    /// <summary>
    /// Average of <see cref="MonthlySurplus.NetMonthlyVariation"/> across <see cref="Points"/>,
    /// measuring typical monthly cash growth or decline rather than average accumulated balance.
    /// </summary>
    public Money AverageMonthlyVariation { get; }

    public bool Equals(MonthlySurplusSeries? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return AsOfDate == other.AsOfDate
               && SelectedMonth == other.SelectedMonth
               && AverageMonthlyVariation == other.AverageMonthlyVariation
               && Points.SequenceEqual(other.Points);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(AsOfDate);
        hash.Add(SelectedMonth);
        hash.Add(AverageMonthlyVariation);
        foreach (var point in Points)
        {
            hash.Add(point);
        }

        return hash.ToHashCode();
    }
}
