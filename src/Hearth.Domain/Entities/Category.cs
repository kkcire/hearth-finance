using Hearth.Domain.Exceptions;

namespace Hearth.Domain.Entities;

/// <summary>
/// A user-defined expense category belonging to an account. Categories can be renamed,
/// archived (hiding them from new entries while keeping historical reports intact),
/// and restored.
/// </summary>
public sealed class Category
{
    private Category(Guid id, Guid accountId, string name)
    {
        Id = id;
        AccountId = accountId;
        Name = name;
    }

    public Guid Id { get; }

    public Guid AccountId { get; }

    public string Name { get; private set; }

    /// <summary>
    /// True when the category can no longer be chosen for new entries, new budgets,
    /// or category reassignments. Existing entries keep pointing to it.
    /// </summary>
    public bool IsArchived { get; private set; }

    public static Category Create(Guid id, Guid accountId, string name)
    {
        DomainValidation.EnsureNotEmpty(id, nameof(id));
        DomainValidation.EnsureNotEmpty(accountId, nameof(accountId), "account id");
        var validName = DomainValidation.ValidateRequiredText(name, nameof(name), DomainValidation.MaxNameLength);

        return new Category(id, accountId, validName);
    }

    public void Rename(string name)
    {
        Name = DomainValidation.ValidateRequiredText(name, nameof(name), DomainValidation.MaxNameLength);
    }

    /// <summary>
    /// Marks the category as archived. Idempotent.
    /// </summary>
    /// <returns>True if the state changed; false if it was already archived.</returns>
    public bool Archive()
    {
        if (IsArchived)
        {
            return false;
        }

        IsArchived = true;
        return true;
    }

    /// <summary>
    /// Makes an archived category active again. Idempotent.
    /// </summary>
    /// <returns>True if the state changed; false if it was already active.</returns>
    public bool Restore()
    {
        if (!IsArchived)
        {
            return false;
        }

        IsArchived = false;
        return true;
    }

    /// <exception cref="DomainException">The category is archived.</exception>
    public void EnsureUsable()
    {
        if (IsArchived)
        {
            throw new DomainException(
                "An archived category cannot be used for new entries, new budgets, or category changes.");
        }
    }
}
