using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Xunit;

namespace Hearth.Tests.Domain.Entities;

public class CategoryTests
{
    private static readonly Guid DefaultOwnerUserId = Guid.NewGuid();

    private static Category NewUserCategory(string name = "Pets", Guid? ownerUserId = null) =>
        Category.CreateForUser(Guid.NewGuid(), ownerUserId ?? DefaultOwnerUserId, name);

    [Fact]
    public void CreateForUser_StoresValuesAndTrimsName_StartsActiveAndNonSystem()
    {
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        var category = Category.CreateForUser(id, ownerId, "  Pets  ");

        Assert.Equal(id, category.Id);
        Assert.Equal(ownerId, category.OwnerUserId);
        Assert.False(category.IsSystem);
        Assert.Equal("Pets", category.Name);
        Assert.False(category.IsArchived);
    }

    [Fact]
    public void CreateSystem_HasNullOwnerAndIsReadOnly()
    {
        var category = Category.CreateSystem(SystemCategories.TransportId, "Transporte");

        Assert.Equal(SystemCategories.TransportId, category.Id);
        Assert.Null(category.OwnerUserId);
        Assert.True(category.IsSystem);
        Assert.False(category.IsArchived);

        Assert.Throws<DomainException>(() => category.Rename("Outro"));
        Assert.Throws<DomainException>(() => category.Archive());
        Assert.Throws<DomainException>(() => category.Restore());
        Assert.Throws<DomainException>(category.EnsureModifiable);
        Assert.Null(Record.Exception(category.EnsureUsable));
        Assert.Equal("Transporte", category.Name);
        Assert.False(category.IsArchived);

        Assert.Throws<ArgumentException>(() => Category.CreateSystem(Guid.Empty, "Transporte"));
        Assert.ThrowsAny<ArgumentException>(() => Category.CreateSystem(Guid.NewGuid(), "   "));
    }

    [Fact]
    public void SystemCategories_ContainsEightUniqueDeterministicCategories()
    {
        var all = SystemCategories.CreateAll();

        Assert.Equal(8, all.Count);
        Assert.Equal(8, all.Select(c => c.Id).Distinct().Count());
        Assert.All(all, c =>
        {
            Assert.True(c.IsSystem);
            Assert.Null(c.OwnerUserId);
            Assert.False(c.IsArchived);
        });
    }

    [Fact]
    public void CreateForUser_EmptyIdOrOwnerUserId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Category.CreateForUser(Guid.Empty, DefaultOwnerUserId, "Pets"));
        Assert.Throws<ArgumentException>(() => Category.CreateForUser(Guid.NewGuid(), Guid.Empty, "Pets"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateForUser_NameIsRequired(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => NewUserCategory(name: name!));
    }

    [Fact]
    public void CreateForUser_NameExceedingMaxLength_Throws()
    {
        var tooLong = new string('a', DomainValidation.MaxNameLength + 1);

        Assert.Throws<ArgumentException>(() => NewUserCategory(name: tooLong));
    }

    [Fact]
    public void Rename_Archive_Restore_WorkForUserCategory()
    {
        var category = NewUserCategory("Pets");

        category.Rename("  Animais  ");
        Assert.Equal("Animais", category.Name);

        Assert.True(category.Archive());
        Assert.True(category.IsArchived);
        Assert.False(category.Archive());
        Assert.Throws<DomainException>(category.EnsureUsable);

        Assert.True(category.Restore());
        Assert.False(category.IsArchived);
        Assert.False(category.Restore());
        Assert.Null(Record.Exception(category.EnsureUsable));
    }
}
