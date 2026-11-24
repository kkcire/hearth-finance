using Hearth.Domain.Exceptions;

namespace Hearth.Domain.Entities;

/// <summary>
/// A credit card purchase condition label used for filtering (e.g., "Própria", "Mãe", "Trabalho").
/// Global system conditions (<see cref="OwnerUserId"/> is null) are read-only and shared across
/// all users; custom conditions belong to a user and can be renamed, archived, or restored.
/// </summary>
public sealed class PurchaseCondition
{
    private PurchaseCondition()
    {
        Name = null!;
    }

    private PurchaseCondition(Guid id, Guid? ownerUserId, string name)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        Name = name;
    }

    public Guid Id { get; }

    /// <summary>Null for global read-only system conditions; set for user-owned custom conditions.</summary>
    public Guid? OwnerUserId { get; }

    public bool IsSystem => OwnerUserId is null;

    public string Name { get; private set; }

    /// <summary>
    /// True when the condition can no longer be chosen for new purchases or reassignments.
    /// Existing purchases keep pointing to it.
    /// </summary>
    public bool IsArchived { get; private set; }

    public static PurchaseCondition CreateSystem(Guid id, string name)
    {
        DomainValidation.EnsureNotEmpty(id, nameof(id));
        var validName = DomainValidation.ValidateRequiredText(name, nameof(name), DomainValidation.MaxNameLength);

        return new PurchaseCondition(id, ownerUserId: null, validName);
    }

    public static PurchaseCondition CreateForUser(Guid id, Guid ownerUserId, string name)
    {
        DomainValidation.EnsureNotEmpty(id, nameof(id));
        DomainValidation.EnsureNotEmpty(ownerUserId, nameof(ownerUserId), "owner user id");
        var validName = DomainValidation.ValidateRequiredText(name, nameof(name), DomainValidation.MaxNameLength);

        return new PurchaseCondition(id, ownerUserId, validName);
    }

    public void Rename(string name)
    {
        EnsureModifiable();
        Name = DomainValidation.ValidateRequiredText(name, nameof(name), DomainValidation.MaxNameLength);
    }

    /// <summary>
    /// Marks a user condition as archived. Idempotent.
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
    /// Makes an archived user condition active again. Idempotent.
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

    /// <exception cref="DomainException">The condition is a read-only system condition.</exception>
    public void EnsureModifiable()
    {
        if (IsSystem)
        {
            throw new DomainException("System purchase conditions are read-only and cannot be modified or deleted.");
        }
    }

    /// <exception cref="DomainException">The condition is archived.</exception>
    public void EnsureUsable()
    {
        if (IsArchived)
        {
            throw new DomainException(
                "An archived purchase condition cannot be used for new purchases or condition changes.");
        }
    }
}
