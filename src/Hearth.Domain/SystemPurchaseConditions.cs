using Hearth.Domain.Entities;

namespace Hearth.Domain;

/// <summary>
/// Deterministic global system purchase conditions available read-only to every user.
/// Fixed GUIDs keep database migrations and in-memory test seeds identical.
/// </summary>
public static class SystemPurchaseConditions
{
    public static readonly Guid OwnId = Guid.Parse("01900000-0000-7000-8000-000000000201");
    public static readonly Guid OthersId = Guid.Parse("01900000-0000-7000-8000-000000000202");

    public static IReadOnlyList<(Guid Id, string Name)> Definitions { get; } =
    [
        (OwnId, "Própria"),
        (OthersId, "Outros"),
    ];

    public static IReadOnlyList<PurchaseCondition> CreateAll() =>
        Definitions.Select(item => PurchaseCondition.CreateSystem(item.Id, item.Name)).ToList();
}
