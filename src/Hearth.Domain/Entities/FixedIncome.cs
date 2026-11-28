using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Entities;

/// <summary>
/// A monthly recurring income (salary, retainer, rental income), active from a start month until
/// an optional end month. Keeps an effective-dated baseline amount history and monthly receipt
/// confirmations whose actual amount may differ from the expected baseline (bonuses/deductions).
/// </summary>
public sealed class FixedIncome
{
    public const int MinExpectedDay = 1;
    public const int MaxExpectedDay = 31;

    private readonly List<FixedIncomeAmountChange> _amountHistory;
    private readonly List<FixedIncomeReceipt> _receipts = [];

    private FixedIncome()
    {
        StartMonth = null!;
        Description = null!;
        _amountHistory = [];
    }

    private FixedIncome(
        Guid id,
        Guid accountId,
        YearMonth startMonth,
        YearMonth? endMonth,
        int expectedDayOfMonth,
        Money amount,
        string description,
        Guid conditionId)
    {
        Id = id;
        AccountId = accountId;
        StartMonth = startMonth;
        EndMonth = endMonth;
        ExpectedDayOfMonth = expectedDayOfMonth;
        Description = description;
        ConditionId = conditionId;
        _amountHistory = [new FixedIncomeAmountChange(startMonth, amount)];
    }

    public Guid Id { get; }

    /// <summary>The account this fixed income belongs to.</summary>
    public Guid AccountId { get; }

    public string Description { get; private set; }

    /// <summary>
    /// Attribution condition (e.g., "Própria" for personal salary, or a third-party condition).
    /// Defaults to <see cref="SystemPurchaseConditions.OwnId"/>.
    /// </summary>
    public Guid ConditionId { get; private set; }

    /// <summary>True when this fixed income represents personal/own income.</summary>
    public bool IsOwn => ConditionId == SystemPurchaseConditions.OwnId;

    /// <summary>Expected calendar day of the month (1..31) when the income usually arrives.</summary>
    public int ExpectedDayOfMonth { get; private set; }

    /// <summary>The first month in which the fixed income is active (inclusive).</summary>
    public YearMonth StartMonth { get; }

    /// <summary>The last month in which the fixed income is active (inclusive). Null while ongoing.</summary>
    public YearMonth? EndMonth { get; private set; }

    /// <summary>Baseline amounts ordered by effective month. The first entry always starts at <see cref="StartMonth"/>.</summary>
    public IReadOnlyCollection<FixedIncomeAmountChange> AmountHistory =>
        _amountHistory.OrderBy(change => change.EffectiveFrom).ToList().AsReadOnly();

    /// <summary>Confirmed monthly receipts ordered by competence month (at most one per month).</summary>
    public IReadOnlyCollection<FixedIncomeReceipt> Receipts =>
        _receipts.OrderBy(receipt => receipt.Month).ToList().AsReadOnly();

    /// <summary>Allocation-free internal read access to receipts for domain calculators.</summary>
    internal IReadOnlyList<FixedIncomeReceipt> GetReceiptEntries() => _receipts;

    public static FixedIncome Create(
        Guid id,
        Account account,
        YearMonth startMonth,
        YearMonth? endMonth,
        int expectedDayOfMonth,
        Money amount,
        string description,
        Guid? conditionId = null)
    {
        ArgumentNullException.ThrowIfNull(account);
        account.EnsureCanRegisterInMonth(startMonth);

        return Create(id, account.Id, startMonth, endMonth, expectedDayOfMonth, amount, description, conditionId);
    }

    public static FixedIncome Create(
        Guid id,
        Guid accountId,
        YearMonth startMonth,
        YearMonth? endMonth,
        int expectedDayOfMonth,
        Money amount,
        string description,
        Guid? conditionId = null)
    {
        DomainValidation.EnsureNotEmpty(id, nameof(id));
        DomainValidation.EnsureNotEmpty(accountId, nameof(accountId), "account id");
        MonthlyAmountSchedule.ValidatePeriod(startMonth, endMonth);
        ValidateExpectedDay(expectedDayOfMonth);
        DomainValidation.EnsurePositiveAmount(amount, nameof(amount), "income amount");
        var validDescription = DomainValidation.ValidateRequiredText(
            description, nameof(description), DomainValidation.MaxDescriptionLength);

        var resolvedConditionId = conditionId ?? SystemPurchaseConditions.OwnId;
        DomainValidation.EnsureNotEmpty(resolvedConditionId, nameof(conditionId), "income condition id");

        return new FixedIncome(
            id,
            accountId,
            startMonth,
            endMonth,
            expectedDayOfMonth,
            amount,
            validDescription,
            resolvedConditionId);
    }

    public bool IsActiveIn(YearMonth month) =>
        MonthlyAmountSchedule.IsActiveIn(StartMonth, EndMonth, month);

    /// <summary>Returns the expected calendar date for the given month, clamped to the month's last day.</summary>
    public DateOnly ExpectedDateIn(YearMonth month)
    {
        ArgumentNullException.ThrowIfNull(month);

        var day = Math.Min(ExpectedDayOfMonth, DateTime.DaysInMonth(month.Year, month.Month));
        return new DateOnly(month.Year, month.Month, day);
    }

    /// <summary>Returns the expected baseline amount for an active month.</summary>
    /// <exception cref="DomainException">The fixed income is not active in <paramref name="month"/>.</exception>
    public Money GetExpectedAmountFor(YearMonth month)
    {
        if (!IsActiveIn(month))
        {
            throw new DomainException("The fixed income is not active in this month.");
        }

        FixedIncomeAmountChange? latest = null;
        foreach (var change in _amountHistory)
        {
            if (change.EffectiveFrom <= month
                && (latest is null || change.EffectiveFrom > latest.EffectiveFrom))
            {
                latest = change;
            }
        }

        return latest is not null
            ? latest.Amount
            : throw new InvalidOperationException("The amount history does not cover the start month.");
    }

    /// <summary>Returns the confirmed receipt for <paramref name="month"/>, or null if not yet received.</summary>
    public FixedIncomeReceipt? GetReceiptFor(YearMonth month)
    {
        ArgumentNullException.ThrowIfNull(month);

        return _receipts.FirstOrDefault(receipt => receipt.Month == month);
    }

    public bool IsReceivedIn(YearMonth month)
    {
        ArgumentNullException.ThrowIfNull(month);

        return _receipts.Any(receipt => receipt.Month == month);
    }

    public void UpdateDetails(string description, int expectedDayOfMonth, Guid conditionId)
    {
        var validDescription = DomainValidation.ValidateRequiredText(
            description, nameof(description), DomainValidation.MaxDescriptionLength);
        ValidateExpectedDay(expectedDayOfMonth);
        DomainValidation.EnsureNotEmpty(conditionId, nameof(conditionId), "income condition id");

        Description = validDescription;
        ExpectedDayOfMonth = expectedDayOfMonth;
        ConditionId = conditionId;
    }

    /// <summary>
    /// Updates the baseline expected amount from <paramref name="effectiveFrom"/> onward without
    /// rewriting the history of earlier months. Replaces any existing scheduled entries at or after
    /// <paramref name="effectiveFrom"/>.
    /// </summary>
    public void ChangeAmountFrom(YearMonth effectiveFrom, Money newAmount)
    {
        ArgumentNullException.ThrowIfNull(effectiveFrom);
        DomainValidation.EnsurePositiveAmount(newAmount, nameof(newAmount), "income amount");

        if (effectiveFrom < StartMonth)
        {
            throw new DomainException("The effective month cannot be before the fixed income's start month.");
        }

        if (EndMonth is not null && effectiveFrom > EndMonth)
        {
            throw new DomainException("The fixed income has already ended before the effective month.");
        }

        _amountHistory.RemoveAll(change => change.EffectiveFrom >= effectiveFrom);
        _amountHistory.Add(new FixedIncomeAmountChange(effectiveFrom, newAmount));
    }

    /// <summary>
    /// Sets the last active month (inclusive). Rejects stopping before any month that already has
    /// a confirmed receipt.
    /// </summary>
    public void StopAt(YearMonth endMonth)
    {
        ArgumentNullException.ThrowIfNull(endMonth);

        if (endMonth < StartMonth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endMonth), endMonth, "The end month cannot be before the start month.");
        }

        if (_receipts.Any(receipt => receipt.Month > endMonth))
        {
            throw new DomainException(
                "A month after the new end month already has a confirmed receipt. Revert the receipt first.");
        }

        EndMonth = endMonth;
    }

    /// <summary>Clears <see cref="EndMonth"/> so the fixed income becomes ongoing again. Idempotent.</summary>
    public bool Reactivate()
    {
        if (EndMonth is null)
        {
            return false;
        }

        EndMonth = null;
        return true;
    }

    public FixedIncomeReceipt ConfirmReceipt(
        Account account,
        YearMonth month,
        DateOnly receivedOn,
        Money actualAmount,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(account);
        account.EnsureCanRegisterInMonth(month);
        account.EnsureCanRegisterOn(receivedOn);

        return ConfirmReceipt(month, receivedOn, actualAmount, today);
    }

    /// <summary>
    /// Records the actual receipt for an active competence month, supporting bonuses or deductions
    /// (<paramref name="actualAmount"/> may differ from the expected baseline).
    /// </summary>
    /// <exception cref="DomainException">
    /// The receipt date is in the future, the fixed income is inactive in <paramref name="month"/>,
    /// or a receipt for <paramref name="month"/> is already confirmed.
    /// </exception>
    public FixedIncomeReceipt ConfirmReceipt(
        YearMonth month,
        DateOnly receivedOn,
        Money actualAmount,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(month);
        DomainValidation.EnsurePositiveAmount(actualAmount, nameof(actualAmount), "income amount");

        if (receivedOn > today)
        {
            throw new DomainException("A fixed income receipt cannot be confirmed on a future date.");
        }

        if (!IsActiveIn(month))
        {
            throw new DomainException("The fixed income is not active in this month.");
        }

        if (_receipts.Any(receipt => receipt.Month == month))
        {
            throw new DomainException("A receipt for this month is already confirmed. Revert it first.");
        }

        var receipt = new FixedIncomeReceipt(month, receivedOn, actualAmount);
        _receipts.Add(receipt);
        return receipt;
    }

    /// <summary>
    /// Removes the confirmed receipt for <paramref name="month"/> so it returns to expected/pending state.
    /// Idempotent.
    /// </summary>
    public bool RevertReceipt(YearMonth month)
    {
        ArgumentNullException.ThrowIfNull(month);

        return _receipts.RemoveAll(receipt => receipt.Month == month) > 0;
    }

    private static void ValidateExpectedDay(int expectedDayOfMonth)
    {
        if (expectedDayOfMonth < MinExpectedDay || expectedDayOfMonth > MaxExpectedDay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedDayOfMonth),
                expectedDayOfMonth,
                $"The expected day of month must be between {MinExpectedDay} and {MaxExpectedDay}.");
        }
    }
}
