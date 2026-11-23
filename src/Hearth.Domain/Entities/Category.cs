using Hearth.Domain.Exceptions;

namespace Hearth.Domain.Entities;

/// <summary>
/// An expense category. Global system categories (<see cref="OwnerUserId"/> is null) are
/// read-only and shared across all users; custom categories belong to a user and can be
/// renamed, archived, restored, or deleted when unused.
/// </summary>
public sealed class Category
{
    private Category()
    {
        Name = null!;
    }

    private Category(Guid id, Guid? ownerUserId, string name)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        Name = name;
    }

    public Guid Id { get; }

    /// <summary>Null for global read-only system categories; set for user-owned custom categories.</summary>
    public Guid? OwnerUserId { get; }

    public bool IsSystem => OwnerUserId is null;

    public string Name { get; private set; }

    /// <summary>
    /// True when the category can no longer be chosen for new entries, new budgets,
    /// or category reassignments. Existing entries keep pointing to it.
    /// </summary>
    public bool IsArchived { get; private set; }

    public static Category CreateSystem(Guid id, string name)
    {
        DomainValidation.EnsureNotEmpty(id, nameof(id));
        var validName = DomainValidation.ValidateRequiredText(name, nameof(name), DomainValidation.MaxNameLength);

        return new Category(id, ownerUserId: null, validName);
    }

    public static Category CreateForUser(Guid id, Guid ownerUserId, string name)
    {
        DomainValidation.EnsureNotEmpty(id, nameof(id));
        DomainValidation.EnsureNotEmpty(ownerUserId, nameof(ownerUserId), "owner user id");
        var validName = DomainValidation.ValidateRequiredText(name, nameof(name), DomainValidation.MaxNameLength);

        return new Category(id, ownerUserId, validName);
    }

    public void Rename(string name)
    {
        EnsureModifiable();
        Name = DomainValidation.ValidateRequiredText(name, nameof(name), DomainValidation.MaxNameLength);
    }

    /// <summary>
    /// Marks a user category as archived. Idempotent.
    /// </summary>
    /// <returns>True if the state changed; false if it was already archived.</returns>
    public bool Archive()
    {
        EnsureModifiable();

        if (IsArchived)
        {
            return false;
        }

        IsArchived = true;
        return true;
    }

    /// <summary>
    /// Makes an archived user category active again. Idempotent.
    /// </summary>
    /// <returns>True if the state changed; false if it was already active.</returns>
    public bool Restore()
    {
        EnsureModifiable();

        if (!IsArchived)
        {
            return false;
        }

        IsArchived = false;
        return true;
    }

    /// <exception cref="DomainException">The category is a read-only system category.</exception>
    public void EnsureModifiable()
    {
        if (IsSystem)
        {
            throw new DomainException("System categories are read-only and cannot be modified or deleted.");
        }
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
