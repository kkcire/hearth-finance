using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.Entities;

public class AccountTests
{
    private static readonly DateOnly DefaultDate = new(2026, 10, 1);
    private static readonly CardBillingRule Rule3 = CardBillingRule.Of(3, 3);

    private static Money M(decimal amount) => Money.FromDecimal(amount);

    private static Account NewAccount(
        string name = "Conta principal",
        decimal initialBalance = 1500.00m,
        DateOnly? initialDate = null,
        CardBillingRule? rule = null) =>
        Account.Create(
            Guid.NewGuid(),
            name,
            M(initialBalance),
            initialDate ?? DefaultDate,
            rule ?? Rule3);

    // ---------- Create ----------

    [Fact]
    public void Create_StoresTheGivenValues_AndTrimsTheName()
    {
        var id = Guid.NewGuid();

        var account = Account.Create(id, "  Conta principal  ", M(1500.00m), DefaultDate, Rule3);

        Assert.Equal(id, account.Id);
        Assert.Equal("Conta principal", account.Name);
        Assert.Equal(M(1500.00m), account.InitialBalance);
        Assert.Equal(DefaultDate, account.InitialBalanceDate);
        Assert.Equal(Rule3, account.BillingRule);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.01)]
    [InlineData(1500.50)]
    [InlineData(-0.01)]
    [InlineData(-250.75)]
    public void Create_AcceptsZeroPositiveAndNegativeInitialBalance(double raw)
    {
        var account = NewAccount(initialBalance: (decimal)raw);

        Assert.Equal(M((decimal)raw), account.InitialBalance);
    }

    [Fact]
    public void Create_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => Account.Create(Guid.Empty, "Conta", M(0m), DefaultDate, Rule3));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_NameIsRequired(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => NewAccount(name: name!));
    }

    [Fact]
    public void Create_NullBillingRule_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => Account.Create(Guid.NewGuid(), "Conta", M(0m), DefaultDate, null!));
    }

    [Fact]
    public void Create_AcceptsTheLimitsOfTheSupportedDates()
    {
        var first = NewAccount(initialDate: DateOnly.MinValue);
        var last = NewAccount(initialDate: DateOnly.MaxValue);

        Assert.True(first.CanRegisterOn(DateOnly.MinValue));
        Assert.True(last.CanRegisterOn(DateOnly.MaxValue));
        Assert.False(last.CanRegisterOn(DateOnly.MaxValue.AddDays(-1)));
    }

    // ---------- CanRegisterOn and EnsureCanRegisterOn ----------

    [Fact]
    public void CanRegisterOn_BeforeTheInitialBalanceDate_IsFalse()
    {
        var account = NewAccount(initialDate: new DateOnly(2026, 10, 1));

        Assert.False(account.CanRegisterOn(new DateOnly(2026, 9, 30)));
    }

    [Fact]
    public void CanRegisterOn_OnTheInitialBalanceDate_IsTrue()
    {
        var account = NewAccount(initialDate: new DateOnly(2026, 10, 1));

        Assert.True(account.CanRegisterOn(new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public void CanRegisterOn_AfterTheInitialBalanceDate_IsTrue()
    {
        var account = NewAccount(initialDate: new DateOnly(2026, 10, 1));

        Assert.True(account.CanRegisterOn(new DateOnly(2026, 10, 2)));
        Assert.True(account.CanRegisterOn(new DateOnly(2040, 1, 1)));
    }

    [Fact]
    public void EnsureCanRegisterOn_BeforeTheInitialBalanceDate_Throws()
    {
        var account = NewAccount(initialDate: new DateOnly(2026, 10, 1));

        Assert.Throws<InvalidOperationException>(
            () => account.EnsureCanRegisterOn(new DateOnly(2026, 9, 30)));
    }

    [Fact]
    public void EnsureCanRegisterOn_OnOrAfterTheInitialBalanceDate_DoesNotThrow()
    {
        var account = NewAccount(initialDate: new DateOnly(2026, 10, 1));

        Assert.Null(Record.Exception(() => account.EnsureCanRegisterOn(new DateOnly(2026, 10, 1))));
        Assert.Null(Record.Exception(() => account.EnsureCanRegisterOn(new DateOnly(2026, 10, 2))));
    }

    // ---------- Rename ----------

    [Fact]
    public void Rename_ChangesAndTrims()
    {
        var account = NewAccount();

        account.Rename("  Nubank  ");

        Assert.Equal("Nubank", account.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_Empty_ThrowsAndKeepsTheName(string? name)
    {
        var account = NewAccount(name: "Conta principal");

        Assert.ThrowsAny<ArgumentException>(() => account.Rename(name!));

        Assert.Equal("Conta principal", account.Name);
    }

    // ---------- UpdateInitialBalance ----------

    [Theory]
    [InlineData(0)]
    [InlineData(2300.40)]
    [InlineData(-100)]
    public void UpdateInitialBalance_ChangesTheAmount_AndKeepsTheDate(double raw)
    {
        var account = NewAccount(initialBalance: 1500.00m, initialDate: new DateOnly(2026, 10, 1));

        account.UpdateInitialBalance(M((decimal)raw));

        Assert.Equal(M((decimal)raw), account.InitialBalance);
        Assert.Equal(new DateOnly(2026, 10, 1), account.InitialBalanceDate);
    }

    [Fact]
    public void UpdateInitialBalance_DoesNotChangeWhichDatesCanBeRegistered()
    {
        var account = NewAccount(initialDate: new DateOnly(2026, 10, 1));

        account.UpdateInitialBalance(M(99.99m));

        Assert.False(account.CanRegisterOn(new DateOnly(2026, 9, 30)));
        Assert.True(account.CanRegisterOn(new DateOnly(2026, 10, 1)));
    }

    // ---------- ChangeBillingRule ----------

    [Fact]
    public void ChangeBillingRule_ReplacesTheRule_AndKeepsTheOtherFields()
    {
        var account = NewAccount();
        var id = account.Id;

        account.ChangeBillingRule(CardBillingRule.Of(15, 15));

        Assert.Equal(CardBillingRule.Of(15, 15), account.BillingRule);
        Assert.Equal(id, account.Id);
        Assert.Equal("Conta principal", account.Name);
        Assert.Equal(M(1500.00m), account.InitialBalance);
        Assert.Equal(DefaultDate, account.InitialBalanceDate);
    }

    [Fact]
    public void ChangeBillingRule_Null_ThrowsAndKeepsTheRule()
    {
        var account = NewAccount(rule: Rule3);

        Assert.Throws<ArgumentNullException>(() => account.ChangeBillingRule(null!));

        Assert.Equal(Rule3, account.BillingRule);
    }

    [Fact]
    public void ChangeBillingRule_MovesTheFreezeDayOfFixedExpenses()
    {
        // The freeze day of fixed expenses is the closing day of the account's card.
        var account = NewAccount(rule: CardBillingRule.Of(3, 3));
        var today = new DateOnly(2026, 10, 10);

        var before = FixedExpense.FirstOpenMonth(today, account.BillingRule);
        account.ChangeBillingRule(CardBillingRule.Of(15, 15));
        var after = FixedExpense.FirstOpenMonth(today, account.BillingRule);

        Assert.Equal(YearMonth.Of(2026, 11), before);
        Assert.Equal(YearMonth.Of(2026, 10), after);
    }

    // ---------- Main invariants ----------

    [Theory]
    [InlineData(2026, 10, 1)]
    [InlineData(2028, 2, 29)]
    [InlineData(2026, 12, 31)]
    [InlineData(2027, 1, 1)]
    public void CanRegisterOn_IsTrueExactlyFromTheInitialBalanceDateOnward(int year, int month, int day)
    {
        var initial = new DateOnly(year, month, day);
        var account = NewAccount(initialDate: initial);

        for (var offset = -800; offset <= 800; offset++)
        {
            var date = initial.AddDays(offset);
            var expected = offset >= 0;
            var context = $"initial={initial:yyyy-MM-dd}, date={date:yyyy-MM-dd}";

            Assert.True(account.CanRegisterOn(date) == expected, $"Wrong CanRegisterOn: {context}");

            var exception = Record.Exception(() => account.EnsureCanRegisterOn(date));

            if (expected)
            {
                Assert.True(exception is null, $"EnsureCanRegisterOn threw unexpectedly: {context}");
            }
            else
            {
                Assert.True(
                    exception is InvalidOperationException,
                    $"EnsureCanRegisterOn did not throw InvalidOperationException: {context}");
            }
        }
    }

    [Fact]
    public void IdAndInitialBalanceDate_NeverChange_UnderRandomOperations()
    {
        var random = new Random(20261026);
        var account = NewAccount();
        var id = account.Id;
        var initialDate = account.InitialBalanceDate;

        // A simple model of what the entity must hold, to compare against.
        var modelName = account.Name;
        var modelBalance = account.InitialBalance;
        var modelRule = account.BillingRule;

        for (var step = 0; step < 3000; step++)
        {
            var context = $"step={step}";

            switch (random.Next(3))
            {
                case 0:
                    modelName = $"Conta {random.Next(1, 100000)}";
                    account.Rename($"  {modelName}  ");
                    break;
                case 1:
                    modelBalance = M(random.Next(-500000, 500000) / 100m);
                    account.UpdateInitialBalance(modelBalance);
                    break;
                default:
                    modelRule = CardBillingRule.Of(random.Next(1, 29), random.Next(1, 29));
                    account.ChangeBillingRule(modelRule);
                    break;
            }

            Assert.True(account.Id == id, $"Id changed: {context}");
            Assert.True(account.InitialBalanceDate == initialDate, $"Initial balance date changed: {context}");
            Assert.True(account.Name == modelName, $"Name differs from the model: {context}");
            Assert.True(account.InitialBalance == modelBalance, $"Initial balance differs from the model: {context}");
            Assert.True(account.BillingRule == modelRule, $"Billing rule differs from the model: {context}");
        }
    }
}