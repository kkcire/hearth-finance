using Hearth.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Hearth.Infrastructure.Persistence.Converters;

public sealed class YearMonthConverter : ValueConverter<YearMonth, int>
{
    public YearMonthConverter()
        : base(
            yearMonth => (yearMonth.Year * 100) + yearMonth.Month,
            value => YearMonth.Of(value / 100, value % 100))
    {
    }
}
