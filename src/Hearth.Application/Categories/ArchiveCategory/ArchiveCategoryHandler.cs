using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.Categories.ArchiveCategory;

public sealed class ArchiveCategoryHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;

    public ArchiveCategoryHandler(
        AccountAccessGuard accountAccess,
        ICategoryRepository categories,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _categories = categories;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> ExecuteAsync(
        ArchiveCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _accountAccess.GetRequiredUserId();

        var category = await _categories.GetByIdAsync(command.CategoryId, cancellationToken)
                       ?? throw new NotFoundException(nameof(Category), command.CategoryId);

        await _accountAccess.EnsureCanAccessEntityAsync(
            category.AccountId, nameof(Category), category.Id, cancellationToken);

        var changed = category.Archive();

        if (changed)
        {
            await _categories.UpdateAsync(category, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }
}
