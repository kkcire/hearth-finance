using Hearth.Domain.ValueObjects;
using Hearth.Web.Services;

namespace Hearth.Tests.Web.Services;

public sealed class HearthFormatTests
{
    [Fact]
    public void Money_FormatsInBrazilianReais()
    {
        var formatted = HearthFormat.Money(1234.56m);

        Assert.Contains("R$", formatted);
        Assert.Contains("1.234,56", formatted);
    }

    [Fact]
    public void Date_FormatsDayMonthYearAndFallback()
    {
        Assert.Equal("20/11/2026", HearthFormat.Date(new DateOnly(2026, 11, 20)));
        Assert.Equal("—", HearthFormat.Date((DateOnly?)null));
        Assert.Equal("Sem data", HearthFormat.Date((DateOnly?)null, "Sem data"));
    }

    [Fact]
    public void MonthYear_CapitalizesPortugueseMonthName()
    {
        Assert.Equal("Novembro de 2026", HearthFormat.MonthYear(YearMonth.Of(2026, 11)));
        Assert.Equal("11/2026", HearthFormat.ShortMonthYear(YearMonth.Of(2026, 11)));
    }

    [Fact]
    public void Percentage_FormatsWithPortugueseDecimalSeparator()
    {
        Assert.Equal("85,5%", HearthFormat.Percentage(85.5m));
    }

    [Fact]
    public void ConditionLabel_ReturnsPortugueseLabelsAndCustomName()
    {
        Assert.Equal("Própria", HearthFormat.ConditionLabel(PurchaseConditionKind.Own));
        Assert.Equal("Outros", HearthFormat.ConditionLabel(PurchaseConditionKind.Others));
        Assert.Equal("Mãe", HearthFormat.ConditionLabel(PurchaseConditionKind.Custom, "Mãe"));
        Assert.Equal("Personalizado", HearthFormat.ConditionLabel(PurchaseConditionKind.Custom, null));
    }

    [Fact]
    public void DateConversions_RoundTripWithoutTimeShift()
    {
        var date = new DateOnly(2026, 11, 20);
        var dateTime = HearthFormat.ToDateTime(date);

        Assert.Equal(new DateTime(2026, 11, 20, 0, 0, 0), dateTime);
        Assert.Equal(date, HearthFormat.ToDateOnly(dateTime));
        Assert.Null(HearthFormat.ToDateTime((DateOnly?)null));
        Assert.Null(HearthFormat.ToDateOnly((DateTime?)null));
    }
}
