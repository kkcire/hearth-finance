using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.ValueObjects;

public class MoneyTests
{
    private static Money M(decimal amount) => Money.FromDecimal(amount);

    // ---------- Creation ----------

    [Theory]
    [InlineData(0)]
    [InlineData(0.01)]
    [InlineData(10)]
    [InlineData(10.5)]
    [InlineData(10.55)]
    [InlineData(-10.55)]
    [InlineData(-0.01)]
    public void FromDecimal_AcceptsUpToTwoDecimalPlaces(double raw)
    {
        var amount = (decimal)raw;

        var money = Money.FromDecimal(amount);

        Assert.Equal(amount, money.Amount);
    }

    [Theory]
    [InlineData(10.005)]
    [InlineData(0.001)]
    [InlineData(-0.001)]
    [InlineData(1.999)]
    public void FromDecimal_RejectsMoreThanTwoDecimalPlaces(double raw)
    {
        var amount = (decimal)raw;

        Assert.Throws<ArgumentException>(() => Money.FromDecimal(amount));
    }

    [Fact]
    public void FromDecimal_DoesNotRoundSilently()
    {
        // 10.004 would round to 10.00; it must be rejected instead.
        Assert.Throws<ArgumentException>(() => Money.FromDecimal(10.004m));
    }

    [Fact]
    public void Default_IsEqualToZero()
    {
        Money money = default;

        Assert.Equal(Money.Zero, money);
        Assert.Equal(0m, money.Amount);
    }

    // ---------- Equality ----------

    [Fact]
    public void Equality_IgnoresTrailingZeros()
    {
        var a = M(10.5m);
        var b = M(10.50m);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentAmountsAreNotEqual()
    {
        Assert.NotEqual(M(10.50m), M(10.51m));
        Assert.True(M(10.50m) != M(10.51m));
    }

    [Fact]
    public void Equality_ZeroWithDifferentScalesIsEqual()
    {
        Assert.Equal(Money.Zero, M(0.00m));
        Assert.Equal(Money.Zero.GetHashCode(), M(0.00m).GetHashCode());
    }

    // ---------- Arithmetic ----------

    [Fact]
    public void Add_IsExact()
    {
        // With double, 0.1 + 0.2 != 0.3. With Money it must be exact.
        Assert.Equal(M(0.30m), M(0.10m) + M(0.20m));
    }

    [Fact]
    public void Subtract_IsExact()
    {
        Assert.Equal(M(0.10m), M(0.30m) - M(0.20m));
    }

    [Fact]
    public void Subtract_CanProduceNegativeResult()
    {
        var result = M(10.00m) - M(25.50m);

        Assert.Equal(M(-15.50m), result);
    }

    [Fact]
    public void Negate_FlipsTheSign()
    {
        Assert.Equal(M(-10.25m), M(10.25m).Negate());
        Assert.Equal(M(10.25m), M(-10.25m).Negate());
        Assert.Equal(Money.Zero, Money.Zero.Negate());
    }

    [Fact]
    public void UnaryMinus_BehavesLikeNegate()
    {
        Assert.Equal(M(-3.33m), -M(3.33m));
    }

    [Fact]
    public void Sum_OfEmptyCollection_IsZero()
    {
        Assert.Equal(Money.Zero, Money.Sum(Array.Empty<Money>()));
    }

    [Fact]
    public void Sum_AddsAllValuesExactly()
    {
        var values = new[] { M(0.10m), M(0.20m), M(0.30m), M(-0.10m) };

        Assert.Equal(M(0.50m), Money.Sum(values));
    }

    [Fact]
    public void Sum_NullCollection_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Money.Sum(null!));
    }

    // ---------- Comparison ----------

    [Fact]
    public void Comparison_Operators()
    {
        var small = M(1.00m);
        var large = M(2.00m);

        Assert.True(small < large);
        Assert.True(large > small);
        Assert.True(small <= large);
        Assert.True(large >= small);
        Assert.True(small <= M(1.00m));
        Assert.True(small >= M(1.00m));
        Assert.False(small > large);
        Assert.False(large < small);
    }

    [Fact]
    public void CompareTo_ReturnsSignedResult()
    {
        Assert.True(M(1.00m).CompareTo(M(2.00m)) < 0);
        Assert.True(M(2.00m).CompareTo(M(1.00m)) > 0);
        Assert.Equal(0, M(1.50m).CompareTo(M(1.5m)));
    }

    [Fact]
    public void Comparison_WorksWithNegativeValues()
    {
        Assert.True(M(-5.00m) < Money.Zero);
        Assert.True(M(-5.00m) < M(-1.00m));
    }

    // ---------- Split ----------

    [Fact]
    public void Split_PutsRemainingCentsOnTheLastPart()
    {
        var parts = M(100.00m).Split(3);

        Assert.Equal(3, parts.Count);
        Assert.Equal(M(33.33m), parts[0]);
        Assert.Equal(M(33.33m), parts[1]);
        Assert.Equal(M(33.34m), parts[2]);
    }

    [Fact]
    public void Split_ExactDivision_HasNoRemainder()
    {
        var parts = M(10.00m).Split(4);

        Assert.Equal(4, parts.Count);
        Assert.All(parts, part => Assert.Equal(M(2.50m), part));
    }

    [Fact]
    public void Split_IntoOnePart_ReturnsTheWholeAmount()
    {
        var parts = M(59.90m).Split(1);

        Assert.Single(parts);
        Assert.Equal(M(59.90m), parts[0]);
    }

    [Fact]
    public void Split_MinimumCentPerPart_Works()
    {
        var parts = M(0.03m).Split(3);

        Assert.All(parts, part => Assert.Equal(M(0.01m), part));
    }

    [Fact]
    public void Split_RemainderLargerThanOneCent_GoesToTheLastPart()
    {
        // 5 cents in 3 parts: base 1 cent, remainder 2 cents.
        var parts = M(0.05m).Split(3);

        Assert.Equal(M(0.01m), parts[0]);
        Assert.Equal(M(0.01m), parts[1]);
        Assert.Equal(M(0.03m), parts[2]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Split_NonPositiveNumberOfParts_Throws(int parts)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => M(100.00m).Split(parts));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public void Split_ZeroOrNegativeAmount_Throws(double raw)
    {
        var money = M((decimal)raw);

        Assert.Throws<InvalidOperationException>(() => money.Split(2));
    }

    [Fact]
    public void Split_DefaultMoney_Throws()
    {
        Money money = default;

        Assert.Throws<InvalidOperationException>(() => money.Split(1));
    }

    [Fact]
    public void Split_AmountTooSmallForEachPartToGetOneCent_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => M(0.02m).Split(3));
    }

    // ---------- Main invariant ----------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(24)]
    [InlineData(36)]
    public void Split_SumOfPartsAlwaysEqualsOriginal(int partsCount)
    {
        for (long cents = partsCount; cents <= 5000; cents++)
        {
            AssertSplitInvariant(cents, partsCount);
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(12)]
    public void Split_SumOfPartsEqualsOriginal_ForLargeAmounts(int partsCount)
    {
        long[] largeAmountsInCents =
        {
            100_000_01,
            123_456_789,
            999_999_999_999,
        };

        foreach (var cents in largeAmountsInCents)
        {
            AssertSplitInvariant(cents, partsCount);
        }
    }

    private static void AssertSplitInvariant(long cents, int partsCount)
    {
        var original = M(cents / 100m);

        var parts = original.Split(partsCount);

        var context = $"amount={original.Amount}, parts={partsCount}";

        Assert.True(parts.Count == partsCount, $"Wrong number of parts: {context}");
        Assert.True(Money.Sum(parts) == original, $"Sum differs from original: {context}");
        Assert.True(parts.All(p => p > Money.Zero), $"Part is not positive: {context}");

        var baseValue = parts[0];
        for (var i = 0; i < partsCount - 1; i++)
        {
            Assert.True(parts[i] == baseValue, $"Parts before the last must be equal: {context}");
        }

        Assert.True(parts[^1] >= baseValue, $"Last part must not be smaller: {context}");
    }
}