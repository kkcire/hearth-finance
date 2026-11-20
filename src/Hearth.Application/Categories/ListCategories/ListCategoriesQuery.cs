namespace Hearth.Application.Categories.ListCategories;

public sealed record ListCategoriesQuery(Guid AccountId, bool IncludeArchived = false);
