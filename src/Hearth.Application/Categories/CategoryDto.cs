using Hearth.Domain.Entities;

namespace Hearth.Application.Categories;

public sealed record CategoryDto(
    Guid Id,
    Guid? OwnerUserId,
    string Name,
    bool IsArchived,
    bool IsSystem)
{
    public static CategoryDto FromEntity(Category category)
    {
        ArgumentNullException.ThrowIfNull(category);

        return new CategoryDto(
            category.Id,
            category.OwnerUserId,
            category.Name,
            category.IsArchived,
            category.IsSystem);
    }
}
