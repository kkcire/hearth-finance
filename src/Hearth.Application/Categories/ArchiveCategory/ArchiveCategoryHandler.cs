using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.Categories.ArchiveCategory;

public sealed class ArchiveCategoryHandler
{
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;

    public ArchiveCategoryHandler(
        ICategoryRepository categories,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _categories = categories;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> ExecuteAsync(
        ArchiveCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await _categories.GetByIdAsync(command.CategoryId, cancellationToken)
                       ?? throw new NotFoundException(nameof(Category), command.CategoryId);

        var changed = category.Archive();

        if (changed)
        {
            await _categories.UpdateAsync(category, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }
}
