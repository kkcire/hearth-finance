using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Entities;

/// <summary>
/// A credit card purchase split into installments. The invoice is never stored: it is
/// calculated from the installments that belong to each cycle.
/// </summary>
public sealed class CreditCardPurchase
{
    private CreditCardPurchase()
    {
        Rule = null!;
        Description = null!;
    }

    private CreditCardPurchase(
        Guid id,
        Guid accountId,
        Guid creditCardId,
        DateOnly date,
        Money amount,
        int installmentCount,
        int initialInstallment,
        CardBillingRule rule,
        Guid conditionId,
        string description,
        Guid categoryId)
    {
        Id = id;
        AccountId = accountId;
        CreditCardId = creditCardId;
        Date = date;
        Amount = amount;
        InstallmentCount = installmentCount;
        InitialInstallment = initialInstallment;
        Rule = rule;
        ConditionId = conditionId;
        Description = description;
        CategoryId = categoryId;
    }

    public Guid Id { get; }

    /// <summary>The account this purchase belongs to. Prepares the model for multiple accounts.</summary>
    public Guid AccountId { get; }

    /// <summary>The credit card used for this purchase.</summary>
    public Guid CreditCardId { get; }

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

    public Guid ConditionId { get; private set; }

    public string Description { get; private set; }

    public Guid CategoryId { get; private set; }

    /// <summary>The cycle of the first installment, according to the purchase date.</summary>
    public BillingCycle FirstCycle => BillingCycle.ForPurchase(Date, Rule);

    /// <summary>The cycle of the first active (non-previously-paid) installment.</summary>
    public BillingCycle FirstActiveCycle => FirstCycle.AddCycles(InitialInstallment - 1);

    public static CreditCardPurchase Create(
        Guid id,
        Guid accountId,
        Guid creditCardId,
        DateOnly date,
        Money amount,
        int installmentCount,
        int initialInstallment,
        CardBillingRule rule,
        Guid conditionId,
        string description,
        Guid categoryId)
    {
        DomainValidation.EnsureNotEmpty(id, nameof(id));
        DomainValidation.EnsureNotEmpty(accountId, nameof(accountId), "account id");
        DomainValidation.EnsureNotEmpty(creditCardId, nameof(creditCardId), "credit card id");
        ArgumentNullException.ThrowIfNull(rule);
        DomainValidation.EnsureNotEmpty(conditionId, nameof(conditionId), "purchase condition id");

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
            creditCardId,
            date,
            amount,
            installmentCount,
            initialInstallment,
            CardBillingRule.Of(rule.ClosingDay, rule.DueDay),
            conditionId,
            validDescription,
            categoryId);
    }

    /// <summary>
    /// Calculates the installments. The amount is split with the remaining cents on the first
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
    /// Returns only the installments that still count (from <see cref="InitialInstallment"/> onward),
    /// excluding installments marked as previously paid.
    /// </summary>
    public IEnumerable<Installment> GetActiveInstallments() =>
        GetInstallments().Where(installment => !installment.IsPreviouslyPaid);

    /// <summary>
    /// True once at least one active installment belongs to a paid invoice
    /// (<see cref="InvoiceStatus.Paid"/>, i.e., on or after <see cref="FirstActiveCycle"/>'s due date).
    /// From then on the amount, date, and deletion are locked to protect settled cash history.
    /// </summary>
    public bool IsLockedAt(DateOnly today) =>
        today >= FirstActiveCycle.DueDate;

    /// <exception cref="DomainException">A linked invoice has already been paid or the amount cannot be split.</exception>
    public void UpdateAmount(Money newAmount, DateOnly today)
    {
        EnsureEditable(today);
        EnsureConsistent(Date, newAmount, InstallmentCount, Rule);

        Amount = newAmount;
    }

    /// <exception cref="DomainException">
    /// A linked invoice has already been paid, or the new date moves the first counting installment into an already paid invoice.
    /// </exception>
    public void UpdateDate(DateOnly newDate, DateOnly today)
    {
        EnsureEditable(today);
        EnsureConsistent(newDate, Amount, InstallmentCount, Rule);

        var targetDueDate = BillingCycle.ForPurchase(newDate, Rule)
            .AddCycles(InitialInstallment - 1)
            .DueDate;

        if (today >= targetDueDate)
        {
            throw new DomainException(
                "The purchase date cannot be moved to a cycle whose invoice has already been paid.");
        }

        Date = newDate;
    }

    public void UpdateCondition(Guid conditionId)
    {
        DomainValidation.EnsureNotEmpty(conditionId, nameof(conditionId), "purchase condition id");

        ConditionId = conditionId;
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
                "The amount and the date cannot be changed after a linked invoice has been paid.");
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