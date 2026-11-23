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
        Condition = null!;
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
        PurchaseCondition condition,
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
        Condition = condition;
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

    public PurchaseCondition Condition { get; private set; }

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
        PurchaseCondition condition,
        string description,
        Guid categoryId)
    {
        DomainValidation.EnsureNotEmpty(id, nameof(id));
        DomainValidation.EnsureNotEmpty(accountId, nameof(accountId), "account id");
        DomainValidation.EnsureNotEmpty(creditCardId, nameof(creditCardId), "credit card id");
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
            creditCardId,
            date,
            amount,
            installmentCount,
            initialInstallment,
            CardBillingRule.Of(rule.ClosingDay, rule.DueDay),
            condition with { },
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
    /// True once the invoice of the first installment that still counts has closed.
    /// From then on the amount and the date can no longer be changed.
    /// </summary>
    public bool IsLockedAt(DateOnly today) =>
        today >= FirstActiveCycle.ClosingDate;

    /// <summary>
    /// Ensures the first active installment's due date is on or after both the account's
    /// initial balance date and <paramref name="today"/>. Purchase dates before the initial
    /// balance date are allowed as long as the first active due date is still open.
    /// </summary>
    /// <exception cref="DomainException">
    /// The first active installment's due date is before the account's initial balance date or before <paramref name="today"/>.
    /// </exception>
    public void EnsureFirstActiveDueDateIsOpen(Account account, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(account);

        EnsureDueDateIsOpen(FirstActiveCycle.DueDate, account, today);
    }

    /// <summary>
    /// Validates that moving this purchase to <paramref name="purchaseDate"/> keeps the first
    /// active installment's due date on or after both the account's initial balance date and <paramref name="today"/>.
    /// </summary>
    /// <exception cref="DomainException">
    /// The resulting first active installment's due date is before the account's initial balance date or before <paramref name="today"/>.
    /// </exception>
    public void EnsureFirstActiveDueDateIsOpen(DateOnly purchaseDate, Account account, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(account);
        EnsureConsistent(purchaseDate, Amount, InstallmentCount, Rule);

        var dueDate = BillingCycle.ForPurchase(purchaseDate, Rule)
            .AddCycles(InitialInstallment - 1)
            .DueDate;

        EnsureDueDateIsOpen(dueDate, account, today);
    }

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

    private void EnsureDueDateIsOpen(DateOnly dueDate, Account account, DateOnly today)
    {
        if (!account.CanRegisterOn(dueDate))
        {
            throw new DomainException(
                $"Installment {InitialInstallment}/{InstallmentCount} due date ({dueDate:yyyy-MM-dd}) cannot be before the initial balance date ({account.InitialBalanceDate:yyyy-MM-dd}).");
        }

        if (dueDate < today)
        {
            throw new DomainException(
                $"Installment {InitialInstallment}/{InstallmentCount} due date ({dueDate:yyyy-MM-dd}) has already passed. Advance the initial installment to skip already-paid cycles.");
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