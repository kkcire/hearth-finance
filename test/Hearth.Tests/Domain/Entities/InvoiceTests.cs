using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.Entities;

public class InvoiceTests
{
    private static readonly CardBillingRule Rule = CardBillingRule.Of(3, 12);

    private static Money M(decimal amount) => Money.FromDecimal(amount);

    private static Money? Amt(decimal amount) => M(amount);

    private static DateOnly Day(int year, int month, int day) => new(year, month, day);

    private static DateOnly? OptDay(int year, int month, int day) => new DateOnly(year, month, day);

    private static BillingCycle CycleOf(int year, int month) =>
        BillingCycle.Closing(YearMonth.Of(year, month), Rule);

    private static Invoice NewInvoice(BillingCycle? cycle = null) =>
        Invoice.Create(Guid.NewGuid(), Guid.NewGuid(), cycle ?? CycleOf(2026, 10));

    private static Invoice NewInvoiceWithCharge(decimal charged)
    {
        var invoice = NewInvoice();
        invoice.InformChargedAmount(M(charged));
        return invoice;
    }

    // ---------- Create ----------

    [Fact]
    public void Create_StoresTheGivenValues()
    {
        var id = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var cycle = CycleOf(2026, 10);

        var invoice = Invoice.Create(id, accountId, cycle);

        Assert.Equal(id, invoice.Id);
        Assert.Equal(accountId, invoice.AccountId);
        Assert.Equal(cycle, invoice.Cycle);
    }

    [Fact]
    public void Create_AlwaysStartsOpen_WithNoChargedAmount()
    {
        var invoice = NewInvoice();

        Assert.Null(invoice.ChargedAmount);
        Assert.Null(invoice.PaidOn);
        Assert.False(invoice.IsPaid);
        Assert.Null(invoice.PaidMonth);
    }

    [Fact]
    public void Create_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => Invoice.Create(Guid.Empty, Guid.NewGuid(), CycleOf(2026, 10)));
    }

    [Fact]
    public void Create_EmptyAccountId_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => Invoice.Create(Guid.NewGuid(), Guid.Empty, CycleOf(2026, 10)));
    }

    [Fact]
    public void Create_NullCycle_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => Invoice.Create(Guid.NewGuid(), Guid.NewGuid(), null!));
    }

    [Fact]
    public void Create_KeepsTheRuleOfTheCycleItWasCreatedWith()
    {
        var ruleFromAnotherCard = CardBillingRule.Of(25, 5);
        var cycle = BillingCycle.Closing(YearMonth.Of(2026, 10), ruleFromAnotherCard);

        var invoice = NewInvoice(cycle);

        Assert.Equal(ruleFromAnotherCard, invoice.Cycle.Rule);
        Assert.Equal(new DateOnly(2026, 11, 5), invoice.Cycle.DueDate);
    }

    // ---------- InformChargedAmount ----------

    [Fact]
    public void InformChargedAmount_StoresTheAmount_WhileOpen()
    {
        var invoice = NewInvoice();

        invoice.InformChargedAmount(M(1234.56m));

        Assert.Equal(Amt(1234.56m), invoice.ChargedAmount);
        Assert.False(invoice.IsPaid);
    }

    [Fact]
    public void InformChargedAmount_Zero_IsAccepted()
    {
        var invoice = NewInvoice();

        invoice.InformChargedAmount(Money.Zero);

        Assert.Equal(Amt(0m), invoice.ChargedAmount);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public void InformChargedAmount_Negative_ThrowsAndKeepsTheAmount(double raw)
    {
        var invoice = NewInvoiceWithCharge(500.00m);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => invoice.InformChargedAmount(M((decimal)raw)));

        Assert.Equal(Amt(500.00m), invoice.ChargedAmount);
    }

    [Fact]
    public void InformChargedAmount_CanBeCorrectedManyTimesWhileOpen()
    {
        var invoice = NewInvoice();

        invoice.InformChargedAmount(M(100.00m));
        invoice.InformChargedAmount(M(250.00m));
        invoice.InformChargedAmount(M(0m));
        invoice.InformChargedAmount(M(310.99m));

        Assert.Equal(Amt(310.99m), invoice.ChargedAmount);
    }

    [Fact]
    public void InformChargedAmount_OnAPaidInvoice_ThrowsAndKeepsTheAmount()
    {
        var invoice = NewInvoiceWithCharge(500.00m);
        invoice.MarkAsPaid(Day(2026, 10, 12));

        Assert.Throws<InvalidOperationException>(() => invoice.InformChargedAmount(M(600.00m)));

        Assert.Equal(Amt(500.00m), invoice.ChargedAmount);
        Assert.True(invoice.IsPaid);
    }

    [Fact]
    public void InformChargedAmount_AfterRevertingThePayment_IsAccepted()
    {
        var invoice = NewInvoiceWithCharge(500.00m);
        invoice.MarkAsPaid(Day(2026, 10, 12));
        invoice.RevertPayment();

        invoice.InformChargedAmount(M(600.00m));

        Assert.Equal(Amt(600.00m), invoice.ChargedAmount);
    }

    // ---------- MarkAsPaid ----------

    [Fact]
    public void MarkAsPaid_WithoutChargedAmount_ThrowsAndStaysOpen()
    {
        var invoice = NewInvoice();

        Assert.Throws<InvalidOperationException>(() => invoice.MarkAsPaid(Day(2026, 10, 12)));

        Assert.False(invoice.IsPaid);
        Assert.Null(invoice.PaidOn);
    }

    [Fact]
    public void MarkAsPaid_WithChargedAmount_BecomesPaidAndReturnsTrue()
    {
        var invoice = NewInvoiceWithCharge(500.00m);

        var changed = invoice.MarkAsPaid(Day(2026, 10, 12));

        Assert.True(changed);
        Assert.True(invoice.IsPaid);
        Assert.Equal(OptDay(2026, 10, 12), invoice.PaidOn);
        Assert.Equal(YearMonth.Of(2026, 10), invoice.PaidMonth);
        Assert.Equal(Amt(500.00m), invoice.ChargedAmount);
    }

    [Fact]
    public void MarkAsPaid_ZeroChargedAmount_CanBePaid()
    {
        var invoice = NewInvoiceWithCharge(0m);

        Assert.True(invoice.MarkAsPaid(Day(2026, 10, 12)));
    }

    [Fact]
    public void MarkAsPaid_Twice_WithTheSameDate_ReturnsFalseAndKeepsTheState()
    {
        var invoice = NewInvoiceWithCharge(500.00m);
        invoice.MarkAsPaid(Day(2026, 10, 12));

        var changed = invoice.MarkAsPaid(Day(2026, 10, 12));

        Assert.False(changed);
        Assert.Equal(OptDay(2026, 10, 12), invoice.PaidOn);
    }

    [Fact]
    public void MarkAsPaid_Twice_WithAnotherDate_KeepsTheOriginalDate()
    {
        var invoice = NewInvoiceWithCharge(500.00m);
        invoice.MarkAsPaid(Day(2026, 10, 12));

        var changed = invoice.MarkAsPaid(Day(2026, 11, 5));

        Assert.False(changed);
        Assert.Equal(OptDay(2026, 10, 12), invoice.PaidOn);
        Assert.Equal(YearMonth.Of(2026, 10), invoice.PaidMonth);
    }

    [Fact]
    public void MarkAsPaid_RepeatedManyTimes_NeverChangesTheFirstDate()
    {
        var invoice = NewInvoiceWithCharge(500.00m);
        Assert.True(invoice.MarkAsPaid(Day(2026, 10, 12)));

        for (var day = 1; day <= 28; day++)
        {
            Assert.False(invoice.MarkAsPaid(Day(2026, 12, day)));
        }

        Assert.Equal(OptDay(2026, 10, 12), invoice.PaidOn);
    }

    [Fact]
    public void MarkAsPaid_AfterTheDueDate_IsAccepted_AndCountsInThePaymentMonth()
    {
        // Cycle closing in October is due on 12/10. Paid late on 02/11: October's invoice, November's cash.
        var invoice = NewInvoiceWithCharge(500.00m);

        var changed = invoice.MarkAsPaid(Day(2026, 11, 2));

        Assert.True(changed);
        Assert.Equal(YearMonth.Of(2026, 10), invoice.Cycle.ClosingMonth);
        Assert.Equal(new DateOnly(2026, 10, 12), invoice.Cycle.DueDate);
        Assert.Equal(YearMonth.Of(2026, 11), invoice.PaidMonth);
    }

    [Fact]
    public void MarkAsPaid_BeforeTheClosing_IsAccepted()
    {
        var invoice = NewInvoiceWithCharge(500.00m);

        var changed = invoice.MarkAsPaid(Day(2026, 9, 20));

        Assert.True(changed);
        Assert.Equal(YearMonth.Of(2026, 9), invoice.PaidMonth);
    }

    [Fact]
    public void MarkAsPaid_AcceptsTheLimitsOfTheSupportedDates()
    {
        var first = NewInvoiceWithCharge(10m);
        var last = NewInvoiceWithCharge(10m);

        first.MarkAsPaid(DateOnly.MinValue);
        last.MarkAsPaid(DateOnly.MaxValue);

        Assert.Equal(YearMonth.Of(1, 1), first.PaidMonth);
        Assert.Equal(YearMonth.Of(9999, 12), last.PaidMonth);
    }

    // ---------- RevertPayment ----------

    [Fact]
    public void RevertPayment_FromPaid_BecomesOpenAndReturnsTrue()
    {
        var invoice = NewInvoiceWithCharge(500.00m);
        invoice.MarkAsPaid(Day(2026, 10, 12));

        var changed = invoice.RevertPayment();

        Assert.True(changed);
        Assert.False(invoice.IsPaid);
        Assert.Null(invoice.PaidOn);
        Assert.Null(invoice.PaidMonth);
    }

    [Fact]
    public void RevertPayment_KeepsTheChargedAmount()
    {
        var invoice = NewInvoiceWithCharge(500.00m);
        invoice.MarkAsPaid(Day(2026, 10, 12));

        invoice.RevertPayment();

        Assert.Equal(Amt(500.00m), invoice.ChargedAmount);
    }

    [Fact]
    public void RevertPayment_Twice_ReturnsFalseTheSecondTime()
    {
        var invoice = NewInvoiceWithCharge(500.00m);
        invoice.MarkAsPaid(Day(2026, 10, 12));

        Assert.True(invoice.RevertPayment());
        Assert.False(invoice.RevertPayment());

        Assert.False(invoice.IsPaid);
    }

    [Fact]
    public void RevertPayment_WhenNeverPaid_ReturnsFalseAndChangesNothing()
    {
        var invoice = NewInvoiceWithCharge(500.00m);

        var changed = invoice.RevertPayment();

        Assert.False(changed);
        Assert.False(invoice.IsPaid);
        Assert.Equal(Amt(500.00m), invoice.ChargedAmount);
    }

    [Fact]
    public void AfterReverting_CanBePaidAgainWithAnotherDate()
    {
        var invoice = NewInvoiceWithCharge(500.00m);
        invoice.MarkAsPaid(Day(2026, 10, 12));
        invoice.RevertPayment();

        var changed = invoice.MarkAsPaid(Day(2026, 10, 20));

        Assert.True(changed);
        Assert.Equal(OptDay(2026, 10, 20), invoice.PaidOn);
    }

    // ---------- DivergenceFrom ----------

    [Fact]
    public void DivergenceFrom_WithoutChargedAmount_IsNull()
    {
        var invoice = NewInvoice();

        Assert.Null(invoice.DivergenceFrom(M(500.00m)));
    }

    [Theory]
    [InlineData(500.00, 500.00, 0.00)]
    [InlineData(520.50, 500.00, 20.50)]
    [InlineData(480.00, 500.00, -20.00)]
    [InlineData(0.00, 0.00, 0.00)]
    [InlineData(0.01, 0.00, 0.01)]
    [InlineData(0.00, 100.00, -100.00)]
    public void DivergenceFrom_IsTheChargedMinusTheCalculatedAmount(
        double charged, double calculated, double expected)
    {
        var invoice = NewInvoiceWithCharge((decimal)charged);

        var divergence = invoice.DivergenceFrom(M((decimal)calculated));

        Assert.Equal(Amt((decimal)expected), divergence);
    }

    [Fact]
    public void DivergenceFrom_WorksOnAPaidInvoice_AndChangesNothing()
    {
        var invoice = NewInvoiceWithCharge(520.50m);
        invoice.MarkAsPaid(Day(2026, 10, 12));

        var divergence = invoice.DivergenceFrom(M(500.00m));

        Assert.Equal(Amt(20.50m), divergence);
        Assert.Equal(Amt(520.50m), invoice.ChargedAmount);
        Assert.True(invoice.IsPaid);
    }

    [Fact]
    public void DivergenceFrom_UsesTheLatestChargedAmount()
    {
        var invoice = NewInvoiceWithCharge(520.50m);
        invoice.InformChargedAmount(M(500.00m));

        Assert.Equal(Amt(0m), invoice.DivergenceFrom(M(500.00m)));
    }

    // ---------- Main invariants ----------

    [Fact]
    public void State_FollowsASimpleModel_UnderRandomOperations()
    {
        var random = new Random(20261027);
        var invoice = NewInvoice();

        // A simple model of what the entity must do, to compare against.
        Money? modelCharged = null;
        DateOnly? modelPaidOn = null;

        for (var step = 0; step < 5000; step++)
        {
            var context = $"step={step}";

            switch (random.Next(3))
            {
                case 0:
                {
                    var amount = M(random.Next(0, 1000000) / 100m);

                    if (modelPaidOn is null)
                    {
                        invoice.InformChargedAmount(amount);
                        modelCharged = amount;
                    }
                    else
                    {
                        Assert.Throws<InvalidOperationException>(() => invoice.InformChargedAmount(amount));
                    }

                    break;
                }

                case 1:
                {
                    var date = Day(2026, 1, 1).AddDays(random.Next(0, 730));

                    if (modelPaidOn is not null)
                    {
                        Assert.False(invoice.MarkAsPaid(date), $"Paying twice must return false: {context}");
                    }
                    else if (modelCharged is null)
                    {
                        Assert.Throws<InvalidOperationException>(() => invoice.MarkAsPaid(date));
                    }
                    else
                    {
                        Assert.True(invoice.MarkAsPaid(date), $"First payment must return true: {context}");
                        modelPaidOn = date;
                    }

                    break;
                }

                default:
                {
                    var expectedChange = modelPaidOn is not null;

                    var changed = invoice.RevertPayment();

                    modelPaidOn = null;

                    Assert.True(changed == expectedChange, $"Wrong result of RevertPayment: {context}");
                    break;
                }
            }

            Assert.True(invoice.IsPaid == invoice.PaidOn.HasValue, $"IsPaid disagrees with PaidOn: {context}");
            Assert.True(invoice.PaidOn == modelPaidOn, $"PaidOn differs from the model: {context}");
            Assert.True(invoice.ChargedAmount == modelCharged, $"ChargedAmount differs from the model: {context}");
            Assert.True(
                !invoice.IsPaid || invoice.ChargedAmount is not null,
                $"A paid invoice has no charged amount: {context}");
        }
    }

    [Fact]
    public void Divergence_PlusTheCalculatedAmount_AlwaysGivesTheChargedAmount()
    {
        var invoice = NewInvoice();

        for (long chargedCents = 0; chargedCents <= 300; chargedCents++)
        {
            var charged = M(chargedCents / 100m);
            invoice.InformChargedAmount(charged);

            for (long calculatedCents = 0; calculatedCents <= 300; calculatedCents++)
            {
                var calculated = M(calculatedCents / 100m);
                var divergence = invoice.DivergenceFrom(calculated);

                Assert.True(divergence is not null, "Divergence must exist once the amount is informed");
                Assert.True(
                    calculated + divergence.Value == charged,
                    $"calculated + divergence != charged: charged={charged.Amount}, calculated={calculated.Amount}");
                Assert.True(
                    (divergence.Value == Money.Zero) == (charged == calculated),
                    $"Zero divergence must mean equal amounts: charged={charged.Amount}, calculated={calculated.Amount}");
            }
        }
    }

    [Fact]
    public void PaidMonth_AlwaysMatchesThePaymentDate_ForEveryDayOfALeapAndANonLeapYear()
    {
        var lastDate = Day(2028, 12, 31);

        for (var date = Day(2027, 1, 1); date <= lastDate; date = date.AddDays(1))
        {
            var invoice = NewInvoiceWithCharge(10m);

            invoice.MarkAsPaid(date);

            Assert.True(
                invoice.PaidMonth == YearMonth.Of(date.Year, date.Month),
                $"PaidMonth does not match the date {date:yyyy-MM-dd}");
        }
    }
}