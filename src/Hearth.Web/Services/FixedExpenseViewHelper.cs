using Hearth.Application.FixedExpenses;
using Hearth.Domain.ValueObjects;

namespace Hearth.Web.Services;

public static class FixedExpenseViewHelper
{
    public static bool IsActiveIn(FixedExpenseDto expense, YearMonth month)
    {
        ArgumentNullException.ThrowIfNull(expense);

        var start = YearMonth.Of(expense.StartYear, expense.StartMonth);
        if (month < start)
        {
            return false;
        }

        if (expense.EndYear.HasValue && expense.EndMonth.HasValue)
        {
            var end = YearMonth.Of(expense.EndYear.Value, expense.EndMonth.Value);
            if (month > end)
            {
                return false;
            }
        }

        return true;
    }

    public static decimal? GetAmountFor(FixedExpenseDto expense, YearMonth month)
    {
        ArgumentNullException.ThrowIfNull(expense);

        if (!IsActiveIn(expense, month))
        {
            return null;
        }

        var entry = expense.AmountHistory
            .LastOrDefault(change => YearMonth.Of(change.EffectiveFromYear, change.EffectiveFromMonth) <= month);

        return entry?.Amount;
    }

    public static decimal GetCurrentOrLatestAmount(FixedExpenseDto expense, YearMonth referenceMonth)
    {
        ArgumentNullException.ThrowIfNull(expense);

        return GetAmountFor(expense, referenceMonth)
            ?? expense.AmountHistory[^1].Amount;
    }

    public static FixedExpensePaymentDto? GetPaymentFor(FixedExpenseDto expense, YearMonth month)
    {
        ArgumentNullException.ThrowIfNull(expense);

        return expense.Payments.FirstOrDefault(payment =>
            payment.Year == month.Year && payment.Month == month.Month);
    }
}
