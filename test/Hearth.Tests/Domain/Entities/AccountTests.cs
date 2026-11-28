using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Xunit;

namespace Hearth.Tests.Domain.Entities;

public class AccountTests
{
    private static readonly DateTimeOffset DefaultCreatedAtUtc = new(2026, 10, 15, 15, 0, 0, TimeSpan.Zero); // 12:00 BRT -> 2026-10-01
    private static readonly Guid DefaultOwnerUserId = Guid.Parse("01900000-0000-7000-8000-000000000001");

    private static Money M(decimal amount) => Money.FromDecimal(amount);

    private static DateTimeOffset UtcFromBrt(int year, int month, int day, int hour = 12, int minute = 0) =>
        new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.FromHours(-3)).ToUniversalTime();

    private static Account NewAccount(
        string name = "Conta principal",
        decimal initialBalance = 1500.00m,
        DateTimeOffset? createdAtUtc = null,
        Guid? ownerUserId = null,
        bool isConfirmed = true) =>
        Account.Create(
            Guid.NewGuid(),
            ownerUserId ?? DefaultOwnerUserId,
            name,
            M(initialBalance),
            createdAtUtc ?? DefaultCreatedAtUtc,
            isConfirmed);

    [Fact]
    public void Create_StoresTheGivenValues_DerivesDay1TrackingStartAndTrimsName()
    {
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        var account = Account.Create(id, ownerId, "  Conta principal  ", M(1500.00m), DefaultCreatedAtUtc);

        Assert.Equal(id, account.Id);
        Assert.Equal(ownerId, account.OwnerUserId);
        Assert.Equal("Conta principal", account.Name);
        Assert.Equal(M(1500.00m), account.InitialBalance);
        Assert.Equal(DefaultCreatedAtUtc, account.CreatedAt);
        Assert.Equal(new DateOnly(2026, 10, 1), account.TrackingStartDate);
        Assert.Equal(YearMonth.Of(2026, 10), account.TrackingStartPeriod);
        Assert.Equal(account.TrackingStartDate, account.InitialBalanceDate);
        Assert.True(account.IsInitialBalanceConfirmed);
    }

    [Theory]
    [InlineData(2026, 12, 1, 0, 1, 2026, 12)]   // Day 1 at 00:01 BRT
    [InlineData(2026, 12, 2, 12, 0, 2026, 12)]  // Day 2 BRT
    [InlineData(2026, 12, 15, 14, 30, 2026, 12)] // Mid-month BRT
    [InlineData(2026, 12, 28, 18, 0, 2026, 12)] // Late month BRT
    [InlineData(2026, 12, 31, 23, 59, 2026, 12)] // Last day of month BRT
    public void Create_Day1MidMonthAndLastDayOfMonth_AllYieldDay1OfThatMonth(
        int year, int month, int day, int hour, int minute,
        int expectedYear, int expectedMonth)
    {
        var createdAtUtc = UtcFromBrt(year, month, day, hour, minute);

        var account = NewAccount(createdAtUtc: createdAtUtc);

        Assert.Equal(new DateOnly(expectedYear, expectedMonth, 1), account.TrackingStartDate);
        Assert.Equal(YearMonth.Of(expectedYear, expectedMonth), account.TrackingStartPeriod);
    }

    [Fact]
    public void Create_UtcMonthBoundary_Nov30At2230Brt_IsDec1Utc_YieldsNov1TrackingStart()
    {
        // Nov 30, 2026 at 22:30 BRT == Dec 1, 2026 at 01:30:00Z
        var utcInstant = new DateTimeOffset(2026, 12, 1, 1, 30, 0, TimeSpan.Zero);

        var account = NewAccount(createdAtUtc: utcInstant);

        Assert.Equal(utcInstant, account.CreatedAt);
        Assert.Equal(new DateOnly(2026, 11, 1), account.TrackingStartDate);
        Assert.Equal(YearMonth.Of(2026, 11), account.TrackingStartPeriod);
    }

    [Fact]
    public void Account_HasNoPublicMethodToOverrideTrackingStartOrMutateOpeningBalancePostCreation()
    {
        var accountType = typeof(Account);

        Assert.Null(accountType.GetMethod("UpdateInitialBalance"));
        Assert.Null(accountType.GetMethod("SetTrackingStartDate"));
        Assert.Null(accountType.GetProperty(nameof(Account.TrackingStartDate))!.SetMethod);
        Assert.DoesNotContain(
            accountType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(m => m.Name == nameof(Account.Create)),
            m => m.GetParameters().Any(p => p.ParameterType == typeof(DateOnly) || p.ParameterType == typeof(YearMonth)));
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
            () => Account.Create(Guid.Empty, DefaultOwnerUserId, "Conta", M(0m), DefaultCreatedAtUtc));
        Assert.Throws<ArgumentException>(
            () => Account.Create(Guid.NewGuid(), Guid.Empty, "Conta", M(0m), DefaultCreatedAtUtc));
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
    public void ConfirmInitialBalance_SetsBalanceOnceAndLocksImmutability()
    {
        var createdAtUtc = UtcFromBrt(2026, 11, 22);
        var account = NewAccount(initialBalance: 0m, createdAtUtc: createdAtUtc, isConfirmed: false);

        account.ConfirmInitialBalance(M(2500.50m));

        Assert.Equal(M(2500.50m), account.InitialBalance);
        Assert.Equal(new DateOnly(2026, 11, 1), account.TrackingStartDate);
        Assert.True(account.IsInitialBalanceConfirmed);

        Assert.Throws<DomainException>(() => account.ConfirmInitialBalance(M(3000m)));
        Assert.Throws<DomainException>(account.SkipInitialBalance);
        Assert.Equal(M(2500.50m), account.InitialBalance);
    }

    [Fact]
    public void SkipInitialBalance_SetsZeroAndLocksConfirmation()
    {
        var createdAtUtc = UtcFromBrt(2026, 11, 22);
        var account = NewAccount(initialBalance: 100m, createdAtUtc: createdAtUtc, isConfirmed: false);

        account.SkipInitialBalance();

        Assert.Equal(Money.Zero, account.InitialBalance);
        Assert.Equal(new DateOnly(2026, 11, 1), account.TrackingStartDate);
        Assert.True(account.IsInitialBalanceConfirmed);
    }

    [Fact]
    public void HistoricalBoundary_RejectsDirectExpensesIncomesAndFixedExpensesBeforeTrackingStart()
    {
        var account = NewAccount(createdAtUtc: UtcFromBrt(2026, 12, 18)); // TrackingStartDate = 2026-12-01

        Assert.False(account.CanRegisterOn(new DateOnly(2026, 11, 30)));
        Assert.True(account.CanRegisterOn(new DateOnly(2026, 12, 1)));
        Assert.True(account.CanRegisterOn(new DateOnly(2026, 12, 18)));

        Assert.Throws<DomainException>(() => DirectExpense.Create(
            Guid.NewGuid(), account, new DateOnly(2026, 11, 30), M(50m), "Antes", SystemCategories.FoodId));
        Assert.Throws<DomainException>(() => Income.Create(
            Guid.NewGuid(), account, new DateOnly(2026, 11, 30), M(1000m), "Antes"));
        Assert.Throws<DomainException>(() => FixedExpense.Create(
            Guid.NewGuid(), account, YearMonth.Of(2026, 11), null, M(200m), "Aluguel", SystemCategories.ServicesId));

        // Day 1 of creation month is allowed immediately even though account was created on Dec 18
        var expense = DirectExpense.Create(
            Guid.NewGuid(), account, new DateOnly(2026, 12, 1), M(50m), "Dia 1", SystemCategories.FoodId);
        var income = Income.Create(
            Guid.NewGuid(), account, new DateOnly(2026, 12, 1), M(1000m), "Dia 1");
        var fixedExp = FixedExpense.Create(
            Guid.NewGuid(), account, YearMonth.Of(2026, 12), null, M(200m), "Aluguel", SystemCategories.ServicesId);

        Assert.Equal(new DateOnly(2026, 12, 1), expense.Date);
        Assert.Equal(new DateOnly(2026, 12, 1), income.Date);
        Assert.Equal(YearMonth.Of(2026, 12), fixedExp.StartMonth);
    }

    [Fact]
    public void CreditCardPurchaseBoundary_AllowsOpenInvoiceAndOngoingInstallments_RejectsFullyPreTrackingPurchases()
    {
        var account = NewAccount(createdAtUtc: UtcFromBrt(2026, 12, 10)); // TrackingStartDate = 2026-12-01
        var cardId = Guid.NewGuid();
        var rule = CardBillingRule.Of(2, 14);

        // Purchase on Nov 20 -> closes Dec 2, due Dec 14 (>= 2026-12-01): ALLOWED
        var openCyclePurchase = CreditCardPurchase.Create(
            Guid.NewGuid(), account.Id, cardId, new DateOnly(2026, 11, 20), M(300m), 1, 1,
            rule, SystemPurchaseConditions.OwnId, "Fatura aberta", SystemCategories.ShoppingId);
        Assert.Null(Record.Exception(() => account.EnsureCanRegisterCreditCardPurchase(openCyclePurchase)));

        // Ongoing 6x purchase from Aug 10 -> installments due Sep..Feb (Dec, Jan, Feb >= 2026-12-01): ALLOWED
        var ongoingPurchase = CreditCardPurchase.Create(
            Guid.NewGuid(), account.Id, cardId, new DateOnly(2026, 8, 10), M(600m), 6, 1,
            rule, SystemPurchaseConditions.OwnId, "Notebook", SystemCategories.ShoppingId);
        Assert.Null(Record.Exception(() => account.EnsureCanRegisterCreditCardPurchase(ongoingPurchase)));

        // Fully historical 1x purchase on Oct 10 -> closes Nov 2, due Nov 14 (< 2026-12-01): REJECTED
        var fullyHistorical = CreditCardPurchase.Create(
            Guid.NewGuid(), account.Id, cardId, new DateOnly(2026, 10, 10), M(150m), 1, 1,
            rule, SystemPurchaseConditions.OwnId, "Passada", SystemCategories.ShoppingId);
        Assert.Throws<DomainException>(() => account.EnsureCanRegisterCreditCardPurchase(fullyHistorical));
    }

    [Fact]
    public void SetBalance_UpdatesSingleDay1OpeningBalanceForPositiveZeroAndNegative_AlwaysAnchoredOnTrackingStartDate()
    {
        var account = NewAccount(initialBalance: 1000m, createdAtUtc: UtcFromBrt(2026, 11, 15)); // TrackingStartDate = 2026-11-01

        var positive = account.SetBalance(
            Guid.NewGuid(), M(1500m), "  Extrato dia 1  ", UtcFromBrt(2026, 11, 15));
        Assert.Equal(M(1500m), account.InitialBalance);
        Assert.Equal(new DateOnly(2026, 11, 1), positive.Date);
        Assert.Equal("Extrato dia 1", positive.Reason);

        var zero = account.SetBalance(
            Guid.NewGuid(), Money.Zero, "Conta zerada", UtcFromBrt(2026, 11, 16));
        Assert.Equal(Money.Zero, account.InitialBalance);
        Assert.Equal(new DateOnly(2026, 11, 1), zero.Date);

        var negative = account.SetBalance(
            Guid.NewGuid(), M(-245.50m), null, UtcFromBrt(2026, 11, 17));
        Assert.Equal(M(-245.50m), account.InitialBalance);
        Assert.Equal(new DateOnly(2026, 11, 1), negative.Date);
        Assert.Null(negative.Reason);
        Assert.Equal(3, account.BalanceAdjustments.Count);
        Assert.All(account.BalanceAdjustments, entry => Assert.Equal(account.TrackingStartDate, entry.Date));

        // Reason length validation still enforced
        var tooLong = new string('x', DomainValidation.MaxDescriptionLength + 1);
        Assert.Throws<DomainException>(() => account.SetBalance(
            Guid.NewGuid(), M(10m), tooLong, UtcFromBrt(2026, 11, 18)));
    }

    [Fact]
    public void RemoveBalanceAdjustment_RevertsInitialBalanceToPreviousDay1EntryOrZero()
    {
        var account = NewAccount(initialBalance: 1000m, createdAtUtc: UtcFromBrt(2026, 11, 15));

        var first = account.SetBalance(Guid.NewGuid(), M(500m), "Primeiro", UtcFromBrt(2026, 11, 15, 10, 0));
        var second = account.SetBalance(Guid.NewGuid(), M(9999m), "Erro de digitação", UtcFromBrt(2026, 11, 15, 11, 0));
        Assert.Equal(M(9999m), account.InitialBalance);

        // Removing the erroneous latest entry reverts InitialBalance to the previous Day-1 entry (500)
        var removedSecond = account.RemoveBalanceAdjustment(second.Id);
        Assert.NotNull(removedSecond);
        Assert.Equal(M(500m), account.InitialBalance);
        Assert.Single(account.BalanceAdjustments);

        // Removing the last remaining entry resets InitialBalance cleanly to R$ 0,00
        var removedFirst = account.RemoveBalanceAdjustment(first.Id);
        Assert.NotNull(removedFirst);
        Assert.Equal(Money.Zero, account.InitialBalance);
        Assert.Empty(account.BalanceAdjustments);

        // Unknown id returns null; empty id throws
        Assert.Null(account.RemoveBalanceAdjustment(Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => account.RemoveBalanceAdjustment(Guid.Empty));
    }

    [Fact]
    public void PreTrackingInstallmentPurchase_WithUnpaidTrackedInstallments_IsNotLockedOnCreation()
    {
        // Account created May 2026 -> TrackingStartDate = 2026-05-01; today = 2026-05-10
        var account = NewAccount(createdAtUtc: UtcFromBrt(2026, 5, 10));
        var cardId = Guid.NewGuid();
        var rule = CardBillingRule.Of(10, 20);
        var today = new DateOnly(2026, 5, 10);

        // 3x purchase made Mar 5 -> inst 1 due Mar 20 (< May 1), inst 2 due Apr 20 (< May 1), inst 3 due May 20 (>= May 1, unpaid!)
        var purchase = CreditCardPurchase.Create(
            Guid.NewGuid(), account.Id, cardId, new DateOnly(2026, 3, 5), M(300m), 3, 1,
            rule, SystemPurchaseConditions.OwnId, "Notebook", SystemCategories.ShoppingId);

        Assert.Null(Record.Exception(() => account.EnsureCanRegisterCreditCardPurchase(purchase)));
        Assert.False(purchase.IsLockedAt(today, account.TrackingStartDate));

        // Amount can still be edited before tracked installment due date (May 20)
        purchase.UpdateAmount(M(330m), today, account.TrackingStartDate);
        Assert.Equal(M(330m), purchase.Amount);

        // Once May 20 arrives, tracked installment 3 is paid -> now locked!
        Assert.True(purchase.IsLockedAt(new DateOnly(2026, 5, 20), account.TrackingStartDate));
        Assert.Throws<DomainException>(() => purchase.UpdateAmount(M(360m), new DateOnly(2026, 5, 20), account.TrackingStartDate));
    }

    [Fact]
    public void Rename_PreservesIdOwnerOpeningBalanceAndTrackingStartDate()
    {
        var account = NewAccount(initialBalance: 1500m, createdAtUtc: UtcFromBrt(2026, 10, 10));

        account.Rename("  Nubank  ");

        Assert.Equal("Nubank", account.Name);
        Assert.Equal(M(1500m), account.InitialBalance);
        Assert.Equal(new DateOnly(2026, 10, 1), account.TrackingStartDate);

        var exactMaxName = new string('a', DomainValidation.MaxNameLength);
        account.Rename($" {exactMaxName} ");
        Assert.Equal(exactMaxName, account.Name);

        Assert.ThrowsAny<ArgumentException>(() => account.Rename("   "));
        Assert.Throws<ArgumentException>(() => account.Rename(new string('b', DomainValidation.MaxNameLength + 1)));
        Assert.Equal(exactMaxName, account.Name);
    }
}
