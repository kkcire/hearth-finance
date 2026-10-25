using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Entities;

/// <summary>
/// An expense paid directly (cash, debit or Pix), outside the credit card.
/// It is always registered as already paid and never enters the invoice.
/// </summary>
public sealed class DirectExpense
{
    private DirectExpense(
        Guid id,
        Guid accountId,
        DateOnly date,
        Money amount,
        string description,
        ExpenseCategory category)
    {
        Id = id;
        AccountId = accountId;
        Date = date;
        Amount = amount;
        Description = description;
        Category = category;
    }

    public Guid Id { get; }

    /// <summary>The account this expense belongs to. Prepares the model for multiple accounts.</summary>
    public Guid AccountId { get; }

    public DateOnly Date { get; private set; }

    public Money Amount { get; private set; }

    public string Description { get; private set; }

    public ExpenseCategory Category { get; private set; }

    /// <summary>The calendar month this expense counts toward.</summary>
    public YearMonth Month => YearMonth.From(Date);

    public static DirectExpense Create(
        Guid id,
        Guid accountId,
        DateOnly date,
        Money amount,
        string description,
        ExpenseCategory category)
    {
        ValidateId(id, nameof(id));
        ValidateId(accountId, nameof(accountId));
        ValidateAmount(amount, nameof(amount));
        var validDescription = ValidateDescription(description);
        ValidateCategory(category);

        return new DirectExpense(id, accountId, date, amount, validDescription, category);
    }

    public void UpdateDate(DateOnly newDate)
    {
        Date = newDate;
    }

    public void UpdateAmount(Money newAmount)
    {
        ValidateAmount(newAmount, nameof(newAmount));

        Amount = newAmount;
    }

    public void UpdateDescription(string description)
    {
        Description = ValidateDescription(description);
    }

    public void UpdateCategory(ExpenseCategory category)
    {
        ValidateCategory(category);

        Category = category;
    }

    private static void ValidateId(Guid id, string paramName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The id must not be empty.", paramName);
        }
    }

    private static void ValidateAmount(Money amount, string paramName)
    {
        if (amount <= Money.Zero)
        {
            throw new ArgumentOutOfRangeException(
                paramName, amount, "The expense amount must be greater than zero.");
        }
    }

    private static string ValidateDescription(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return description.Trim();
    }

    private static void ValidateCategory(ExpenseCategory category)
    {
        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(
                nameof(category), category, "The category is not a known category.");
        }
    }
}