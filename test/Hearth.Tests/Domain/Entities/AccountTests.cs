using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.Entities;

public class AccountTests
{
    private static readonly DateOnly DefaultDate = new(2026, 10, 1);
    private static readonly Guid DefaultOwnerUserId = Guid.Parse("01900000-0000-7000-8000-000000000001");

    private static Money M(decimal amount) => Money.FromDecimal(amount);

    private static Account NewAccount(
        string name = "Conta principal",
        decimal initialBalance = 1500.00m,
        DateOnly? initialDate = null,
        Guid? ownerUserId = null,
        bool isConfirmed = false) =>
        Account.Create(
            Guid.NewGuid(),
            ownerUserId ?? DefaultOwnerUserId,
            name,
            M(initialBalance),
            initialDate ?? DefaultDate,
            isConfirmed);

    [Fact]
    public void Create_StoresTheGivenValues_AndTrimsTheName()
    {
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        var account = Account.Create(id, ownerId, "  Conta principal  ", M(1500.00m), DefaultDate);

        Assert.Equal(id, account.Id);
        Assert.Equal(ownerId, account.OwnerUserId);
        Assert.Equal("Conta principal", account.Name);
        Assert.Equal(M(1500.00m), account.InitialBalance);
        Assert.Equal(DefaultDate, account.InitialBalanceDate);
        Assert.False(account.IsInitialBalanceConfirmed);
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
    public void Create_EmptyIdOrOwnerUserId_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => Account.Create(Guid.Empty, DefaultOwnerUserId, "Conta", M(0m), DefaultDate));
        Assert.Throws<ArgumentException>(
            () => Account.Create(Guid.NewGuid(), Guid.Empty, "Conta", M(0m), DefaultDate));
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
    public void Create_NameExceedingMaxLength_Throws()
    {
        var tooLong = new string('a', DomainValidation.MaxNameLength + 1);

        Assert.Throws<ArgumentException>(() => NewAccount(name: tooLong));
    }

    [Fact]
    public void ConfirmInitialBalance_WithPastOrTodayDate_SetsBalanceDateAndLocksConfirmation()
    {
        var today = new DateOnly(2026, 11, 22);
        var account = NewAccount(initialBalance: 0m, initialDate: today, isConfirmed: false);

        account.ConfirmInitialBalance(M(2500.50m), new DateOnly(2026, 11, 1), today);

        Assert.Equal(M(2500.50m), account.InitialBalance);
        Assert.Equal(new DateOnly(2026, 11, 1), account.InitialBalanceDate);
        Assert.True(account.IsInitialBalanceConfirmed);
    }

    [Fact]
    public void ConfirmInitialBalance_WithFutureDate_ThrowsDomainExceptionAndLeavesUnconfirmed()
    {
        var today = new DateOnly(2026, 11, 22);
        var account = NewAccount(initialBalance: 0m, initialDate: today, isConfirmed: false);

        Assert.Throws<DomainException>(() =>
            account.ConfirmInitialBalance(M(500m), new DateOnly(2026, 11, 23), today));

        Assert.False(account.IsInitialBalanceConfirmed);
        Assert.Equal(Money.Zero, account.InitialBalance);
        Assert.Equal(today, account.InitialBalanceDate);
    }

    [Fact]
    public void ConfirmInitialBalance_WhenAlreadyConfirmed_ThrowsDomainException()
    {
        var today = new DateOnly(2026, 11, 22);
        var account = NewAccount(initialBalance: 100m, initialDate: new DateOnly(2026, 11, 1), isConfirmed: true);

        Assert.Throws<DomainException>(() =>
            account.ConfirmInitialBalance(M(200m), new DateOnly(2026, 11, 5), today));
    }

    [Fact]
    public void SkipInitialBalance_SetsZeroFromTodayAndMarksConfirmed()
    {
        var today = new DateOnly(2026, 11, 22);
        var account = NewAccount(initialBalance: 100m, initialDate: new DateOnly(2026, 10, 1), isConfirmed: false);

        account.SkipInitialBalance(today);

        Assert.Equal(Money.Zero, account.InitialBalance);
        Assert.Equal(today, account.InitialBalanceDate);
        Assert.True(account.IsInitialBalanceConfirmed);
    }

    [Fact]
    public void CanRegisterOn_AndEnsureCanRegisterOn_RespectInitialBalanceDate()
    {
        var account = NewAccount(initialDate: new DateOnly(2026, 10, 1));

        Assert.False(account.CanRegisterOn(new DateOnly(2026, 9, 30)));
        Assert.True(account.CanRegisterOn(new DateOnly(2026, 10, 1)));
        Assert.True(account.CanRegisterOn(new DateOnly(2026, 10, 2)));

        Assert.Throws<DomainException>(() => account.EnsureCanRegisterOn(new DateOnly(2026, 9, 30)));
        Assert.Null(Record.Exception(() => account.EnsureCanRegisterOn(new DateOnly(2026, 10, 1))));
    }

    [Fact]
    public void Rename_AndUpdateInitialBalance_PreserveIdOwnerAndInitialBalanceDate()
    {
        var account = NewAccount(initialBalance: 1500m, initialDate: new DateOnly(2026, 10, 1), isConfirmed: true);

        account.Rename("  Nubank  ");
        account.UpdateInitialBalance(M(-100m));

        Assert.Equal("Nubank", account.Name);
        Assert.Equal(M(-100m), account.InitialBalance);
        Assert.Equal(new DateOnly(2026, 10, 1), account.InitialBalanceDate);
        Assert.True(account.IsInitialBalanceConfirmed);
    }
}
