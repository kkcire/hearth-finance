using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;

namespace Hearth.Application.Categories.ListCategories;

public sealed class ListCategoriesHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly ICategoryRepository _categories;

    public ListCategoriesHandler(
        AccountAccessGuard accountAccess,
        ICategoryRepository categories)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(categories);

        _accountAccess = accountAccess;
        _categories = categories;
    }

    public async Task<IReadOnlyList<CategoryDto>> ExecuteAsync(
        ListCategoriesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        _ = await _accountAccess.GetAuthorizedAccountAsync(query.AccountId, cancellationToken);

        var categories = await _categories.GetByAccountAsync(query.AccountId, cancellationToken);

        return categories
            .Where(category => query.IncludeArchived || !category.IsArchived)
            .OrderBy(category => category.Name, StringComparer.OrdinalIgnoreCase)
            .Select(CategoryDto.FromEntity)
            .ToList();
    }
}
