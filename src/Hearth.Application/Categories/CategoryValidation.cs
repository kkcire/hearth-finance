using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;

namespace Hearth.Application.Categories;

internal static class CategoryValidation
{
    public static async Task EnsureUsableForAccountAsync(
        ICategoryRepository categories,
        Guid accountId,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        var category = await categories.GetByIdAsync(categoryId, cancellationToken)
                       ?? throw new NotFoundException(nameof(Category), categoryId);

        if (category.AccountId != accountId)
        {
            throw new DomainException("The category does not belong to this account.");
        }

        category.EnsureUsable();
    }
}
