using Hearth.Domain.Entities;

namespace Hearth.Application.Categories;

public sealed record CategoryDto(
    Guid Id,
    Guid AccountId,
    string Name,
    bool IsArchived)
{
    internal static CategoryDto FromEntity(Category category) =>
        new(category.Id, category.AccountId, category.Name, category.IsArchived);
}
