using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.Categories.RestoreCategory;

public sealed class RestoreCategoryHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;

    public RestoreCategoryHandler(
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
        RestoreCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var ownerUserId = _accountAccess.GetRequiredUserId();

        var category = await _categories.GetByIdAsync(command.CategoryId, cancellationToken)
                       ?? throw new NotFoundException(nameof(Category), command.CategoryId);

        if (category.IsSystem)
        {
            category.EnsureModifiable();
        }

        if (category.OwnerUserId != ownerUserId)
        {
            throw new NotFoundException(nameof(Category), command.CategoryId);
        }

        var changed = category.Restore();

        if (changed)
        {
            await _categories.UpdateAsync(category, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }
}
