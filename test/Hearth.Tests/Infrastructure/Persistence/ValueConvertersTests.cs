using Hearth.Domain.ValueObjects;
using Hearth.Infrastructure.Persistence.Converters;
using Xunit;

namespace Hearth.Tests.Infrastructure.Persistence;

public class ValueConvertersTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(0.01)]
    [InlineData(1234.56)]
    [InlineData(-987.65)]
    public void MoneyConverter_RoundTripsExactDecimal(double raw)
    {
        var converter = new MoneyConverter();
        var money = Money.FromDecimal((decimal)raw);

        var providerValue = (decimal)converter.ConvertToProvider(money)!;
        var restored = (Money)converter.ConvertFromProvider(providerValue)!;

        Assert.Equal((decimal)raw, providerValue);
        Assert.Equal(money, restored);
    }

    [Theory]
    [InlineData(1, 1, 101)]
    [InlineData(2026, 1, 202601)]
    [InlineData(2026, 11, 202611)]
    [InlineData(9999, 12, 999912)]
    public void YearMonthConverter_RoundTripsAsYyyyMmInteger(int year, int month, int expectedInt)
    {
        var converter = new YearMonthConverter();
        var ym = YearMonth.Of(year, month);

        var providerValue = (int)converter.ConvertToProvider(ym)!;
        var restored = (YearMonth)converter.ConvertFromProvider(providerValue)!;

        Assert.Equal(expectedInt, providerValue);
        Assert.Equal(ym, restored);
    }
}
