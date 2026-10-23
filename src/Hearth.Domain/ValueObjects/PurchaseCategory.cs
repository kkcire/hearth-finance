namespace Hearth.Domain.ValueObjects;

/// <summary>
/// Fixed purchase categories. Values start at 1 so that <c>default</c> (0) is never a valid category.
/// </summary>
public enum PurchaseCategory
{
    Transport = 1,      // Transporte
    Food = 2,           // Alimentação
    Health = 3,         // Saúde
    Education = 4,      // Educação
    Entertainment = 5,  // Entretenimento
    Services = 6,       // Serviços
    Shopping = 7,       // Compras
    Personal = 8,       // Pessoal
}