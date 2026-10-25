using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Entities;

/// <summary>
/// A credit card purchase split into installments. The invoice is never stored: it is
/// calculated from the installments that belong to each cycle.
/// </summary>
public sealed class CreditCardPurchase
{
    private CreditCardPurchase(
        Guid id,
        Guid accountId,
        DateOnly date,
        Money amount,
        int installmentCount,
        int initialInstallment,
        CardBillingRule rule,
        PurchaseCondition condition,
        string description,
        PurchaseCategory category)
    {
        Id = id;
        AccountId = accountId;
        Date = date;
        Amount = amount;
        InstallmentCount = installmentCount;
        InitialInstallment = initialInstallment;
        Rule = rule;
        Condition = condition;
        Description = description;
        Category = category;
    }

    public Guid Id { get; }

    /// <summary>The account this purchase belongs to. Prepares the model for multiple accounts.</summary>
    public Guid AccountId { get; }

    /// <summary>The original purchase date. It decides the cycle of the first installment.</summary>
    public DateOnly Date { get; private set; }

    /// <summary>The original total amount of the purchase.</summary>
    public Money Amount { get; private set; }

    public int InstallmentCount { get; }

    /// <summary>
    /// The first installment that still counts. Installments before it are considered paid.
    /// It is 1 for a new purchase.
    /// </summary>
    public int InitialInstallment { get; }

    public CardBillingRule Rule { get; }

    public PurchaseCondition Condition { get; }

    public string Description { get; private set; }

    public PurchaseCategory Category { get; private set; }

    /// <summary>The cycle of the first installment, according to the purchase date.</summary>
    public BillingCycle FirstCycle => BillingCycle.ForPurchase(Date, Rule);

    public static CreditCardPurchase Create(
        Guid id,
        Guid accountId,
        DateOnly date,
        Money amount,
        int installmentCount,
        int initialInstallment,
        CardBillingRule rule,
        PurchaseCondition condition,
        string description,
        PurchaseCategory category)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The id must not be empty.", nameof(id));
        }

        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("The account id must not be empty.", nameof(accountId));
        }

        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(condition);

        if (installmentCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(installmentCount), installmentCount, "There must be at least one installment.");
        }

        if (initialInstallment < 1 || initialInstallment > installmentCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialInstallment),
                initialInstallment,
                "The initial installment must be between 1 and the number of installments.");
        }

        var validDescription = ValidateDescription(description);
        ValidateCategory(category);
        EnsureConsistent(date, amount, installmentCount, rule);

        return new CreditCardPurchase(
            id,
            accountId,
            date,
            amount,
            installmentCount,
            initialInstallment,
            rule,
            condition,
            validDescription,
            category);
    }

    /// <summary>
    /// Calculates the installments. The amount is split with the remaining cents on the last
    /// installment, and installment k belongs to the first cycle plus k - 1 cycles.
    /// </summary>
    public IReadOnlyList<Installment> GetInstallments()
    {
        var amounts = Amount.Split(InstallmentCount);
        var firstCycle = FirstCycle;

        var installments = new Installment[InstallmentCount];
        for (var i = 0; i < InstallmentCount; i++)
        {
            var number = i + 1;
            installments[i] = new Installment(
                number,
                amounts[i],
                firstCycle.AddCycles(i),
                isPreviouslyPaid: number < InitialInstallment);
        }

        return installments;
    }

    /// <summary>
    /// True once the invoice of the first installment that still counts has closed.
    /// From then on the amount and the date can no longer be changed.
    /// </summary>
    public bool IsLockedAt(DateOnly today) =>
        today >= FirstCycle.AddCycles(InitialInstallment - 1).ClosingDate;

    /// <exception cref="InvalidOperationException">The invoice has already closed.</exception>
    public void UpdateAmount(Money newAmount, DateOnly today)
    {
        EnsureEditable(today);
        EnsureConsistent(Date, newAmount, InstallmentCount, Rule);

        Amount = newAmount;
    }

    /// <exception cref="InvalidOperationException">The invoice has already closed.</exception>
    public void UpdateDate(DateOnly newDate, DateOnly today)
    {
        EnsureEditable(today);
        EnsureConsistent(newDate, Amount, InstallmentCount, Rule);

        Date = newDate;
    }

    public void UpdateDescription(string description)
    {
        Description = ValidateDescription(description);
    }

    public void UpdateCategory(PurchaseCategory category)
    {
        ValidateCategory(category);

        Category = category;
    }

    private void EnsureEditable(DateOnly today)
    {
        if (IsLockedAt(today))
        {
            throw new InvalidOperationException(
                "The amount and the date cannot be changed after the invoice has closed.");
        }
    }

    // Fails early if the amount cannot be split or if the cycles fall outside the supported years.
    private static void EnsureConsistent(
        DateOnly date, Money amount, int installmentCount, CardBillingRule rule)
    {
        if (amount <= Money.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount), amount, "The purchase amount must be greater than zero.");
        }

        _ = amount.Split(installmentCount);
        _ = BillingCycle.ForPurchase(date, rule).AddCycles(installmentCount - 1);
    }

    private static string ValidateDescription(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return description.Trim();
    }

    private static void ValidateCategory(PurchaseCategory category)
    {
        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(
                nameof(category), category, "The category is not a known purchase category.");
        }
    }
}