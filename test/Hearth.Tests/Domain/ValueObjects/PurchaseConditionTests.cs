using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.ValueObjects;

public class PurchaseConditionTests
{
    [Fact]
    public void Own_HasKindOwn_AndNoName()
    {
        Assert.Equal(PurchaseConditionKind.Own, PurchaseCondition.Own.Kind);
        Assert.Null(PurchaseCondition.Own.Name);
    }

    [Fact]
    public void Others_HasKindOthers_AndNoName()
    {
        Assert.Equal(PurchaseConditionKind.Others, PurchaseCondition.Others.Kind);
        Assert.Null(PurchaseCondition.Others.Name);
    }

    [Fact]
    public void Custom_HasKindCustom_AndTheGivenName()
    {
        var condition = PurchaseCondition.Custom("Ana");

        Assert.Equal(PurchaseConditionKind.Custom, condition.Kind);
        Assert.Equal("Ana", condition.Name);
    }

    [Fact]
    public void Custom_TrimsTheName()
    {
        Assert.Equal("Ana", PurchaseCondition.Custom("  Ana  ").Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Custom_RequiresAName(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => PurchaseCondition.Custom(name!));
    }

    [Fact]
    public void Equality_SameCustomName_AreEqualWithSameHash()
    {
        var a = PurchaseCondition.Custom("Ana");
        var b = PurchaseCondition.Custom(" Ana ");

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentConditions_AreNotEqual()
    {
        Assert.NotEqual(PurchaseCondition.Custom("Ana"), PurchaseCondition.Custom("Bia"));
        Assert.NotEqual(PurchaseCondition.Own, PurchaseCondition.Others);
        Assert.NotEqual(PurchaseCondition.Own, PurchaseCondition.Custom("Own"));
    }

    [Fact]
    public void Own_AndOthers_AreStableValues()
    {
        Assert.Equal(PurchaseCondition.Own, PurchaseCondition.Own);
        Assert.Equal(PurchaseCondition.Others, PurchaseCondition.Others);
    }
}