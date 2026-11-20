using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Entities;

/// <summary>
/// An expense paid directly (cash, debit or Pix), outside the credit card.
/// It is always registered as already paid and never enters the invoice.
/// </summary>
public sealed class DirectExpense
{
    private DirectExpense()
    {
        Description = null!;
    }

    private DirectExpense(
        Guid id,
        Guid accountId,
        DateOnly date,
        Money amount,
        string description,
        Guid categoryId)
    {
        Id = id;
        AccountId = accountId;
        Date = date;
        Amount = amount;
        Description = description;
        CategoryId = categoryId;
    }

    public Guid Id { get; }

    /// <summary>The account this expense belongs to. Prepares the model for multiple accounts.</summary>
    public Guid AccountId { get; }

    public DateOnly Date { get; private set; }

    public Money Amount { get; private set; }

    public string Description { get; private set; }

    public Guid CategoryId { get; private set; }

    /// <summary>The calendar month this expense counts toward.</summary>
    public YearMonth Month => YearMonth.From(Date);

    public static DirectExpense Create(
        Guid id,
        Guid accountId,
        DateOnly date,
        Money amount,
        string description,
        Guid categoryId)
    {
        DomainValidation.EnsureNotEmpty(id, nameof(id));
        DomainValidation.EnsureNotEmpty(accountId, nameof(accountId), "account id");
        DomainValidation.EnsurePositiveAmount(amount, nameof(amount), "expense amount");
        var validDescription = DomainValidation.ValidateRequiredText(
            description, nameof(description), DomainValidation.MaxDescriptionLength);
        DomainValidation.EnsureNotEmpty(categoryId, nameof(categoryId), "category id");

        return new DirectExpense(id, accountId, date, amount, validDescription, categoryId);
    }

    public void UpdateDate(DateOnly newDate)
    {
        Date = newDate;
    }

    public void UpdateAmount(Money newAmount)
    {
        DomainValidation.EnsurePositiveAmount(newAmount, nameof(newAmount), "expense amount");

        Amount = newAmount;
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