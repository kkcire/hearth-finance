using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;

namespace Hearth.Application.Categories.RenameCategory;

public sealed class RenameCategoryHandler
{
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;

    public RenameCategoryHandler(
        ICategoryRepository categories,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _categories = categories;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        RenameCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await _categories.GetByIdAsync(command.CategoryId, cancellationToken)
                       ?? throw new NotFoundException(nameof(Category), command.CategoryId);

        var originalName = category.Name;
        category.Rename(command.Name);

        if (await _categories.ExistsWithNameAsync(
                category.AccountId,
                category.Name,
                excludeCategoryId: category.Id,
                cancellationToken: cancellationToken))
        {
            category.Rename(originalName);
            throw new DomainException($"A category named '{command.Name.Trim()}' already exists in this account.");
        }

        await _categories.UpdateAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
