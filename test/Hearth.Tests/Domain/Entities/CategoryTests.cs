using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Xunit;

namespace Hearth.Tests.Domain.Entities;

public class CategoryTests
{
    private static Category NewCategory(string name = "Alimentação", Guid? accountId = null) =>
        Category.Create(Guid.NewGuid(), accountId ?? Guid.NewGuid(), name);

    [Fact]
    public void Create_StoresValuesAndTrimsName_StartsActive()
    {
        var id = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var category = Category.Create(id, accountId, "  Alimentação  ");

        Assert.Equal(id, category.Id);
        Assert.Equal(accountId, category.AccountId);
        Assert.Equal("Alimentação", category.Name);
        Assert.False(category.IsArchived);
    }

    [Fact]
    public void Create_EmptyIdOrAccountId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Category.Create(Guid.Empty, Guid.NewGuid(), "Transporte"));
        Assert.Throws<ArgumentException>(() => Category.Create(Guid.NewGuid(), Guid.Empty, "Transporte"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_NameIsRequired(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => NewCategory(name: name!));
    }

    [Fact]
    public void Create_NameExceedingMaxLength_Throws()
    {
        var tooLong = new string('a', DomainValidation.MaxNameLength + 1);

        Assert.Throws<ArgumentException>(() => NewCategory(name: tooLong));
    }

    [Fact]
    public void Rename_ChangesAndTrimsName()
    {
        var category = NewCategory("Transporte");

        category.Rename("  Mobilidade  ");

        Assert.Equal("Mobilidade", category.Name);
    }

    [Fact]
    public void Rename_ExceedingMaxLength_ThrowsAndKeepsOriginalName()
    {
        var category = NewCategory("Transporte");
        var tooLong = new string('a', DomainValidation.MaxNameLength + 1);

        Assert.Throws<ArgumentException>(() => category.Rename(tooLong));
        Assert.Equal("Transporte", category.Name);
    }

    [Fact]
    public void Archive_AndRestore_AreIdempotent()
    {
        var category = NewCategory();

        Assert.True(category.Archive());
        Assert.True(category.IsArchived);
        Assert.False(category.Archive());

        Assert.True(category.Restore());
        Assert.False(category.IsArchived);
        Assert.False(category.Restore());
    }

    [Fact]
    public void EnsureUsable_ActiveDoesNotThrow_ArchivedThrowsDomainException()
    {
        var category = NewCategory();

        Assert.Null(Record.Exception(category.EnsureUsable));

        category.Archive();

        Assert.Throws<DomainException>(category.EnsureUsable);

        category.Restore();

        Assert.Null(Record.Exception(category.EnsureUsable));
    }
}
