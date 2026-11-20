namespace Hearth.Application.Categories.CreateCategory;

public sealed record CreateCategoryCommand(Guid AccountId, string Name);
