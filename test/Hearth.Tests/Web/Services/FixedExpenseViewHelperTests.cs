using Hearth.Application.FixedExpenses;
using Hearth.Domain.ValueObjects;
using Hearth.Web.Services;

namespace Hearth.Tests.Web.Services;

public sealed class FixedExpenseViewHelperTests
{
    private static FixedExpenseDto CreateSample(int? endYear = null, int? endMonth = null) =>
        new(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            StartYear: 2026,
            StartMonth: 3,
            EndYear: endYear,
            EndMonth: endMonth,
            Description: "Internet",
            CategoryId: Guid.CreateVersion7(),
            AmountHistory:
            [
                new FixedExpenseAmountChangeDto(2026, 3, 100m),
                new FixedExpenseAmountChangeDto(2026, 6, 120m),
                new FixedExpenseAmountChangeDto(2026, 10, 150m),
            ],
            Payments:
            [
                new FixedExpensePaymentDto(2026, 6, new DateOnly(2026, 6, 10)),
            ]);

    [Fact]
    public void IsActiveIn_RespectsStartAndOptionalEndMonth()
    {
        var expense = CreateSample(endYear: 2026, endMonth: 11);

        Assert.False(FixedExpenseViewHelper.IsActiveIn(expense, YearMonth.Of(2026, 2)));
        Assert.True(FixedExpenseViewHelper.IsActiveIn(expense, YearMonth.Of(2026, 3)));
        Assert.True(FixedExpenseViewHelper.IsActiveIn(expense, YearMonth.Of(2026, 11)));
        Assert.False(FixedExpenseViewHelper.IsActiveIn(expense, YearMonth.Of(2026, 12)));
    }

    [Fact]
    public void GetAmountFor_ReturnsEffectiveAmountForActiveMonthOrNullWhenInactive()
    {
        var expense = CreateSample(endYear: 2026, endMonth: 11);

        Assert.Null(FixedExpenseViewHelper.GetAmountFor(expense, YearMonth.Of(2026, 2)));
        Assert.Equal(100m, FixedExpenseViewHelper.GetAmountFor(expense, YearMonth.Of(2026, 5)));
        Assert.Equal(120m, FixedExpenseViewHelper.GetAmountFor(expense, YearMonth.Of(2026, 6)));
        Assert.Equal(120m, FixedExpenseViewHelper.GetAmountFor(expense, YearMonth.Of(2026, 9)));
        Assert.Equal(150m, FixedExpenseViewHelper.GetAmountFor(expense, YearMonth.Of(2026, 10)));
        Assert.Null(FixedExpenseViewHelper.GetAmountFor(expense, YearMonth.Of(2026, 12)));
    }

    [Fact]
    public void GetPaymentFor_ReturnsMatchingMonthPaymentOrNull()
    {
        var expense = CreateSample();

        var payment = FixedExpenseViewHelper.GetPaymentFor(expense, YearMonth.Of(2026, 6));
        Assert.NotNull(payment);
        Assert.Equal(new DateOnly(2026, 6, 10), payment.PaidOn);

        Assert.Null(FixedExpenseViewHelper.GetPaymentFor(expense, YearMonth.Of(2026, 7)));
    }

    [Fact]
    public void GetCurrentOrLatestAmount_FallsBackToLatestAmountWhenInactive()
    {
        var expense = CreateSample(endYear: 2026, endMonth: 11);

        Assert.Equal(150m, FixedExpenseViewHelper.GetCurrentOrLatestAmount(expense, YearMonth.Of(2026, 12)));
        Assert.Equal(100m, FixedExpenseViewHelper.GetCurrentOrLatestAmount(expense, YearMonth.Of(2026, 4)));
    }
}
