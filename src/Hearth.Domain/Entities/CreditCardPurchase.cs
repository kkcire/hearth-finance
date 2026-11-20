using Hearth.Domain.Exceptions;
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
        Guid categoryId)
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
        CategoryId = categoryId;
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

    public PurchaseCondition Condition { get; private set; }

    public string Description { get; private set; }

    public Guid CategoryId { get; private set; }

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
        Guid categoryId)
    {
        DomainValidation.EnsureNotEmpty(id, nameof(id));
        DomainValidation.EnsureNotEmpty(accountId, nameof(accountId), "account id");
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

        var validDescription = DomainValidation.ValidateRequiredText(
            description, nameof(description), DomainValidation.MaxDescriptionLength);
        DomainValidation.EnsureNotEmpty(categoryId, nameof(categoryId), "category id");
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
            categoryId);
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

    /// <exception cref="DomainException">The invoice has already closed or the amount cannot be split.</exception>
    public void UpdateAmount(Money newAmount, DateOnly today)
    {
        EnsureEditable(today);
        EnsureConsistent(Date, newAmount, InstallmentCount, Rule);

        Amount = newAmount;
    }

    /// <exception cref="DomainException">
    /// The invoice has already closed, or the new date moves the first counting installment into an already closed cycle.
    /// </exception>
    public void UpdateDate(DateOnly newDate, DateOnly today)
    {
        EnsureEditable(today);
        EnsureConsistent(newDate, Amount, InstallmentCount, Rule);

        var targetClosingDate = BillingCycle.ForPurchase(newDate, Rule)
            .AddCycles(InitialInstallment - 1)
            .ClosingDate;

        if (today >= targetClosingDate)
        {
            throw new DomainException(
                "The purchase date cannot be moved to a cycle whose invoice has already closed.");
        }

        Date = newDate;
    }

    public void UpdateCondition(PurchaseCondition condition)
    {
        ArgumentNullException.ThrowIfNull(condition);

        Condition = condition;
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

    private void EnsureEditable(DateOnly today)
    {
        if (IsLockedAt(today))
        {
            throw new DomainException(
                "The amount and the date cannot be changed after the invoice has closed.");
        }
    }

    // Fails early if the amount cannot be split or if the cycles fall outside the supported years.
    private static void EnsureConsistent(
        DateOnly date, Money amount, int installmentCount, CardBillingRule rule)
    {
        DomainValidation.EnsurePositiveAmount(amount, nameof(amount), "purchase amount");

        _ = amount.Split(installmentCount);
        _ = BillingCycle.ForPurchase(date, rule).AddCycles(installmentCount - 1);
    }
}