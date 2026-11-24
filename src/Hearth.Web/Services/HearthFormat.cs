using System.Globalization;
using Hearth.Domain.ValueObjects;

namespace Hearth.Web.Services;

public static class HearthFormat
{
    public static CultureInfo PtBr { get; } = CultureInfo.GetCultureInfo("pt-BR");

    public static string Money(decimal amount) =>
        amount.ToString("C2", PtBr);

    public static string Date(DateOnly date) =>
        date.ToString("dd/MM/yyyy", PtBr);

    public static string Date(DateOnly? date, string fallback = "—") =>
        date.HasValue ? Date(date.Value) : fallback;

    public static string MonthYear(int year, int month)
    {
        var formatted = new DateOnly(year, month, 1).ToString("MMMM 'de' yyyy", PtBr);

        return char.ToUpper(formatted[0], PtBr) + formatted[1..];
    }

    public static string MonthYear(YearMonth month) =>
        MonthYear(month.Year, month.Month);

    public static string ShortMonthYear(int year, int month) =>
        $"{month:D2}/{year:D4}";

    public static string ShortMonthYear(YearMonth month) =>
        ShortMonthYear(month.Year, month.Month);

    public static string Percentage(decimal value) =>
        $"{value.ToString("N1", PtBr)}%";

    public static DateTime ToDateTime(DateOnly date) =>
        date.ToDateTime(TimeOnly.MinValue);

    public static DateTime? ToDateTime(DateOnly? date) =>
        date.HasValue ? ToDateTime(date.Value) : null;

    public static DateOnly ToDateOnly(DateTime date) =>
        DateOnly.FromDateTime(date);

    public static DateOnly? ToDateOnly(DateTime? date) =>
        date.HasValue ? ToDateOnly(date.Value) : null;
}
