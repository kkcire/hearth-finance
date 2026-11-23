using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.Categories;

internal static class CategoryValidation
{
    public static async Task EnsureUsableForUserAsync(
        ICategoryRepository categories,
        Guid ownerUserId,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        var category = await categories.GetByIdAsync(categoryId, cancellationToken)
                       ?? throw new NotFoundException(nameof(Category), categoryId);

        if (!category.IsSystem && category.OwnerUserId != ownerUserId)
        {
            throw new NotFoundException(nameof(Category), categoryId);
        }

        category.EnsureUsable();
    }
}
