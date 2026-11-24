using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Xunit;

namespace Hearth.Tests.Domain.Entities;

public class PurchaseConditionTests
{
    private static readonly Guid UserId = Guid.Parse("01900000-0000-7000-8000-000000000001");

    [Fact]
    public void CreateSystem_CreatesReadOnlyGlobalCondition_AndTrimsName()
    {
        var condition = PurchaseCondition.CreateSystem(SystemPurchaseConditions.OwnId, "  Própria  ");

        Assert.Equal(SystemPurchaseConditions.OwnId, condition.Id);
        Assert.Null(condition.OwnerUserId);
        Assert.True(condition.IsSystem);
        Assert.Equal("Própria", condition.Name);
        Assert.False(condition.IsArchived);
    }

    [Fact]
    public void CreateForUser_StoresOwnerAndTrimsName()
    {
        var id = Guid.NewGuid();
        var condition = PurchaseCondition.CreateForUser(id, UserId, "  Mãe  ");

        Assert.Equal(id, condition.Id);
        Assert.Equal(UserId, condition.OwnerUserId);
        Assert.False(condition.IsSystem);
        Assert.Equal("Mãe", condition.Name);
        Assert.False(condition.IsArchived);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RequiresName(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => PurchaseCondition.CreateSystem(Guid.NewGuid(), name!));
        Assert.ThrowsAny<ArgumentException>(() => PurchaseCondition.CreateForUser(Guid.NewGuid(), UserId, name!));
    }

    [Fact]
    public void Create_RejectsEmptyGuidsAndOverlengthNames()
    {
        var tooLong = new string('a', DomainValidation.MaxNameLength + 1);

        Assert.Throws<ArgumentException>(() => PurchaseCondition.CreateSystem(Guid.Empty, "Própria"));
        Assert.Throws<ArgumentException>(() => PurchaseCondition.CreateForUser(Guid.Empty, UserId, "Mãe"));
        Assert.Throws<ArgumentException>(() => PurchaseCondition.CreateForUser(Guid.NewGuid(), Guid.Empty, "Mãe"));
        Assert.Throws<ArgumentException>(() => PurchaseCondition.CreateForUser(Guid.NewGuid(), UserId, tooLong));
    }

    [Fact]
    public void SystemCondition_CannotBeRenamedArchivedOrRestored()
    {
        var system = PurchaseCondition.CreateSystem(SystemPurchaseConditions.OwnId, "Própria");

        Assert.Throws<DomainException>(() => system.Rename("Outra"));
        Assert.Throws<DomainException>(() => system.Archive());
        Assert.Throws<DomainException>(() => system.Restore());
        Assert.Throws<DomainException>(system.EnsureModifiable);
        Assert.Null(Record.Exception(system.EnsureUsable));
    }

    [Fact]
    public void UserCondition_SupportsRenameArchiveRestoreAndUsableCheck()
    {
        var condition = PurchaseCondition.CreateForUser(Guid.NewGuid(), UserId, "Mãe");

        condition.Rename("  Trabalho  ");
        Assert.Equal("Trabalho", condition.Name);

        Assert.True(condition.Archive());
        Assert.False(condition.Archive());
        Assert.True(condition.IsArchived);
        Assert.Throws<DomainException>(condition.EnsureUsable);

        Assert.True(condition.Restore());
        Assert.False(condition.Restore());
        Assert.False(condition.IsArchived);
        Assert.Null(Record.Exception(condition.EnsureUsable));
    }
}
