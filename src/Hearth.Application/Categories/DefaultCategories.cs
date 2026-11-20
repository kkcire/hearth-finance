namespace Hearth.Application.Categories;

/// <summary>
/// Default category names seeded when a new account is created.
/// Stored as editable user data in Portuguese.
/// </summary>
public static class DefaultCategories
{
    public static IReadOnlyList<string> Names { get; } =
    [
        "Transporte",
        "Alimentação",
        "Saúde",
        "Educação",
        "Entretenimento",
        "Serviços",
        "Compras",
        "Pessoal",
    ];
}
