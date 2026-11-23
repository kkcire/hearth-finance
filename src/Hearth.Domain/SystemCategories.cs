using Hearth.Domain.Entities;

namespace Hearth.Domain;

/// <summary>
/// Deterministic global system categories available read-only to every user.
/// Fixed GUIDs keep database migrations and in-memory test seeds identical.
/// </summary>
public static class SystemCategories
{
    public static readonly Guid TransportId = Guid.Parse("01900000-0000-7000-8000-000000000101");
    public static readonly Guid FoodId = Guid.Parse("01900000-0000-7000-8000-000000000102");
    public static readonly Guid HealthId = Guid.Parse("01900000-0000-7000-8000-000000000103");
    public static readonly Guid EducationId = Guid.Parse("01900000-0000-7000-8000-000000000104");
    public static readonly Guid EntertainmentId = Guid.Parse("01900000-0000-7000-8000-000000000105");
    public static readonly Guid ServicesId = Guid.Parse("01900000-0000-7000-8000-000000000106");
    public static readonly Guid ShoppingId = Guid.Parse("01900000-0000-7000-8000-000000000107");
    public static readonly Guid PersonalId = Guid.Parse("01900000-0000-7000-8000-000000000108");

    public static IReadOnlyList<(Guid Id, string Name)> Definitions { get; } =
    [
        (TransportId, "Transporte"),
        (FoodId, "Alimentação"),
        (HealthId, "Saúde"),
        (EducationId, "Educação"),
        (EntertainmentId, "Entretenimento"),
        (ServicesId, "Serviços"),
        (ShoppingId, "Compras"),
        (PersonalId, "Pessoal"),
    ];

    public static IReadOnlyList<Category> CreateAll() =>
        Definitions.Select(item => Category.CreateSystem(item.Id, item.Name)).ToList();
}
