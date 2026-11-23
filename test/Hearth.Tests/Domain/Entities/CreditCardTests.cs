using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.Entities;

public sealed class CreditCardTests
{
    private static readonly Guid DefaultAccountId = Guid.NewGuid();
    private static readonly CardBillingRule Rule3_12 = CardBillingRule.Of(3, 12);

    [Fact]
    public void Create_WithOptionalNameAndLimit_TrimsNameAndStartsActive()
    {
        var id = Guid.NewGuid();
        var card = CreditCard.Create(id, DefaultAccountId, Rule3_12, "  Nubank  ", Money.FromDecimal(5000m));

        Assert.Equal(id, card.Id);
        Assert.Equal(DefaultAccountId, card.AccountId);
        Assert.Equal("Nubank", card.Name);
        Assert.Equal(Rule3_12, card.BillingRule);
        Assert.Equal(Money.FromDecimal(5000m), card.Limit);
        Assert.False(card.IsArchived);
    }

    [Fact]
    public void Create_WithoutNameOrLimit_NormalizesBlankNameToNull()
    {
        var card = CreditCard.Create(Guid.NewGuid(), DefaultAccountId, Rule3_12, "   ", null);

        Assert.Null(card.Name);
        Assert.Null(card.Limit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Create_NonPositiveLimit_Throws(decimal rawLimit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreditCard.Create(Guid.NewGuid(), DefaultAccountId, Rule3_12, "Card", Money.FromDecimal(rawLimit)));
    }

    [Fact]
    public void Create_NameExceedingMaxLength_Throws()
    {
        var tooLong = new string('a', DomainValidation.MaxNameLength + 1);

        Assert.Throws<ArgumentException>(() =>
            CreditCard.Create(Guid.NewGuid(), DefaultAccountId, Rule3_12, tooLong, null));
    }

    [Fact]
    public void Create_InvalidIdentifiersOrNullRule_Throw()
    {
        Assert.Throws<ArgumentException>(() => CreditCard.Create(Guid.Empty, DefaultAccountId, Rule3_12));
        Assert.Throws<ArgumentException>(() => CreditCard.Create(Guid.NewGuid(), Guid.Empty, Rule3_12));
        Assert.Throws<ArgumentNullException>(() => CreditCard.Create(Guid.NewGuid(), DefaultAccountId, null!));
    }

    [Fact]
    public void UpdateDetails_UpdatesRuleNameAndBothNullAndNonNullLimit()
    {
        var card = CreditCard.Create(Guid.NewGuid(), DefaultAccountId, Rule3_12, "Nubank", Money.FromDecimal(3000m));

        card.UpdateDetails(CardBillingRule.Of(10, 20), "  Inter  ", Money.FromDecimal(4500m));
        Assert.Equal(CardBillingRule.Of(10, 20), card.BillingRule);
        Assert.Equal("Inter", card.Name);
        Assert.Equal(Money.FromDecimal(4500m), card.Limit);
        Assert.Null(Record.Exception(card.EnsureActive));

        card.UpdateDetails(CardBillingRule.Of(10, 20), "   ", null);
        Assert.Null(card.Name);
        Assert.Null(card.Limit);

        Assert.Throws<ArgumentNullException>(() => card.UpdateDetails(null!, "Inter", null));
        Assert.Throws<ArgumentOutOfRangeException>(() => card.UpdateDetails(Rule3_12, "Inter", Money.Zero));
    }

    [Fact]
    public void Archive_IsIdempotent_AndBlocksFutureUpdatesOrEnsureActiveWithoutMutatingState()
    {
        var card = CreditCard.Create(Guid.NewGuid(), DefaultAccountId, Rule3_12, "Nubank", Money.FromDecimal(3000m));

        Assert.True(card.Archive());
        Assert.True(card.IsArchived);
        Assert.False(card.Archive());

        Assert.Throws<DomainException>(card.EnsureActive);
        Assert.Throws<DomainException>(() => card.UpdateDetails(CardBillingRule.Of(5, 15), "Novo", null));
        Assert.Equal(Rule3_12, card.BillingRule);
        Assert.Equal("Nubank", card.Name);
        Assert.Equal(Money.FromDecimal(3000m), card.Limit);
    }
}
