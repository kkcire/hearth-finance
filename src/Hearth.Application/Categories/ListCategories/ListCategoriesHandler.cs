using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.Categories.ListCategories;

public sealed class ListCategoriesHandler
{
    private readonly IAccountRepository _accounts;
    private readonly ICategoryRepository _categories;

    public ListCategoriesHandler(
        IAccountRepository accounts,
        ICategoryRepository categories)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(categories);

        _accounts = accounts;
        _categories = categories;
    }

    public async Task<IReadOnlyList<CategoryDto>> ExecuteAsync(
        ListCategoriesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        _ = await _accounts.GetByIdAsync(query.AccountId, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), query.AccountId);

        var categories = await _categories.GetByAccountAsync(query.AccountId, cancellationToken);

        return categories
            .Where(category => query.IncludeArchived || !category.IsArchived)
            .OrderBy(category => category.Name, StringComparer.OrdinalIgnoreCase)
            .Select(CategoryDto.FromEntity)
            .ToList();
    }
}
