using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Entities;

/// <summary>
/// A monthly recurring expense (rent, internet, subscriptions), active from a start month until
/// an optional end month. Past calendar months are frozen and never change: edits to the amount
/// keep a history and apply from the current calendar month onward.
/// </summary>
public sealed class FixedExpense
{
    private readonly List<AmountChange> _amountHistory;
    private readonly List<FixedExpensePayment> _payments = [];

    private FixedExpense()
    {
        StartMonth = null!;
        Description = null!;
        _amountHistory = [];
    }

    private FixedExpense(
        Guid id,
        Guid accountId,
        YearMonth startMonth,
        YearMonth? endMonth,
        Money amount,
        string description,
        Guid categoryId)
    {
        Id = id;
        AccountId = accountId;
        StartMonth = startMonth;
        EndMonth = endMonth;
        Description = description;
        CategoryId = categoryId;
        _amountHistory = [new AmountChange(startMonth, amount)];
    }

    public Guid Id { get; }

    /// <summary>The account this expense belongs to. Prepares the model for multiple accounts.</summary>
    public Guid AccountId { get; }

    /// <summary>The first month in which the expense is active.</summary>
    public YearMonth StartMonth { get; }

    /// <summary>The last month in which the expense is active (inclusive). Null while it has no end.</summary>
    public YearMonth? EndMonth { get; private set; }

    public string Description { get; private set; }

    public Guid CategoryId { get; private set; }

    /// <summary>Amounts in order of effective month. The first entry always starts at <see cref="StartMonth"/>.</summary>
    public IReadOnlyList<AmountChange> AmountHistory =>
        _amountHistory.OrderBy(change => change.EffectiveFrom).ToList().AsReadOnly();

    /// <summary>The payment date of each paid month.</summary>
    public IReadOnlyDictionary<YearMonth, DateOnly> Payments =>
        _payments.ToDictionary(payment => payment.Month, payment => payment.PaidOn).AsReadOnly();

    public static FixedExpense Create(
        Guid id,
        Guid accountId,
        YearMonth startMonth,
        YearMonth? endMonth,
        Money amount,
        string description,
        Guid categoryId)
    {
        DomainValidation.EnsureNotEmpty(id, nameof(id));
        DomainValidation.EnsureNotEmpty(accountId, nameof(accountId), "account id");
        ArgumentNullException.ThrowIfNull(startMonth);

        if (endMonth is not null && endMonth < startMonth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endMonth), endMonth, "The end month cannot be before the start month.");
        }

        DomainValidation.EnsurePositiveAmount(amount, nameof(amount), "expense amount");
        var validDescription = DomainValidation.ValidateRequiredText(
            description, nameof(description), DomainValidation.MaxDescriptionLength);
        DomainValidation.EnsureNotEmpty(categoryId, nameof(categoryId), "category id");

        return new FixedExpense(id, accountId, startMonth, endMonth, amount, validDescription, categoryId);
    }

    /// <summary>
    /// The first month that can still be changed: the current calendar month.
    /// Past calendar months are frozen and never change.
    /// </summary>
    public static YearMonth FirstOpenMonth(DateOnly today) => YearMonth.From(today);

    public bool IsActiveIn(YearMonth month)
    {
        ArgumentNullException.ThrowIfNull(month);

        return month >= StartMonth && (EndMonth is null || month <= EndMonth);
    }

    /// <summary>The amount of the given month, or null if the expense is not active in it.</summary>
    public Money? AmountIn(YearMonth month)
    {
        if (!IsActiveIn(month))
        {
            return null;
        }

        AmountChange? latest = null;
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

    /// <summary>
    /// Changes the amount from the first open month onward, keeping the history of earlier months.
    /// A later edit replaces any change already scheduled from that month on.
    /// </summary>
    /// <returns>The month from which the new amount applies.</returns>
    /// <exception cref="DomainException">
    /// The expense has already ended for the open months, or a month affected by the change is already paid.
    /// </exception>
    public YearMonth ChangeAmount(Money newAmount, DateOnly today)
    {
        DomainValidation.EnsurePositiveAmount(newAmount, nameof(newAmount), "expense amount");

        var firstOpen = FirstOpenMonth(today);
        var effectiveFrom = firstOpen > StartMonth ? firstOpen : StartMonth;

        if (EndMonth is not null && effectiveFrom > EndMonth)
        {
            throw new DomainException(
                "The expense has already ended for every month that can still be changed.");
        }

        if (_payments.Any(payment => payment.Month >= effectiveFrom))
        {
            throw new DomainException(
                "A month affected by this change is already paid. Revert the payment first.");
        }

        _amountHistory.RemoveAll(change => change.EffectiveFrom >= effectiveFrom);
        _amountHistory.Add(new AmountChange(effectiveFrom, newAmount));

        return effectiveFrom;
    }

    /// <summary>
    /// Sets the last active month. Only months that are still open can start or stop being active.
    /// Does nothing if the end month is already the given one.
    /// </summary>
    /// <exception cref="DomainException">The change would affect a frozen or paid month.</exception>
    public void End(YearMonth lastMonth, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(lastMonth);

        if (lastMonth < StartMonth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lastMonth), lastMonth, "The last month cannot be before the start month.");
        }

        if (EndMonth == lastMonth)
        {
            return;
        }

        var lastUnchangedMonth =
            EndMonth is { } currentEnd && currentEnd < lastMonth ? currentEnd : lastMonth;

        if (lastUnchangedMonth < YearMonth.Of(YearMonth.MaxYear, 12))
        {
            var firstChangedMonth = lastUnchangedMonth.AddMonths(1);

            if (firstChangedMonth < FirstOpenMonth(today))
            {
                throw new DomainException("Months that are already frozen cannot be changed.");
            }
        }

        if (_payments.Any(payment => payment.Month > lastMonth))
        {
            throw new DomainException(
                "A month after the new last month is already paid. Revert the payment first.");
        }

        EndMonth = lastMonth;
    }

    /// <summary>
    /// Marks the month as paid. Idempotent: if it is already paid, nothing changes and the
    /// original date is kept, so repeating the operation never counts it twice.
    /// </summary>
    /// <returns>True if the state changed; false if the month was already paid.</returns>
    /// <exception cref="DomainException">The expense is not active in the month.</exception>
    public bool MarkAsPaid(YearMonth month, DateOnly paidOn)
    {
        ArgumentNullException.ThrowIfNull(month);

        if (!IsActiveIn(month))
        {
            throw new DomainException("The expense is not active in this month.");
        }

        if (_payments.Any(payment => payment.Month == month))
        {
            return false;
        }

        _payments.Add(new FixedExpensePayment(month, paidOn));
        return true;
    }

    /// <summary>
    /// Takes the payment of the month back, to fix a wrong mark. Idempotent.
    /// </summary>
    /// <returns>True if the state changed; false if the month was not paid.</returns>
    public bool RevertPayment(YearMonth month)
    {
        ArgumentNullException.ThrowIfNull(month);

        return _payments.RemoveAll(payment => payment.Month == month) > 0;
    }

    public bool IsPaid(YearMonth month)
    {
        ArgumentNullException.ThrowIfNull(month);

        return _payments.Any(payment => payment.Month == month);
    }

    /// <summary>The date the month was paid, or null if it was not. It decides the month in the real balance.</summary>
    public DateOnly? PaidOn(YearMonth month)
    {
        ArgumentNullException.ThrowIfNull(month);

        var payment = _payments.FirstOrDefault(item => item.Month == month);
        return payment?.PaidOn;
    }

    public void UpdateDescription(string description)
    {
        Description = DomainValidation.ValidateRequiredText(
            description, nameof(description), DomainValidation.MaxDescriptionLength);
    }

    public void UpdateCategory(Guid categoryId)
    {
        DomainValidation.EnsureNotEmpty(categoryId, nameof(categoryId), "category id");

        CategoryId = categoryId;
    }
}