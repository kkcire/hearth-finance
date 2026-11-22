using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;

namespace Hearth.Application.Categories.RenameCategory;

public sealed class RenameCategoryHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;

    public RenameCategoryHandler(
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

    public async Task ExecuteAsync(
        RenameCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _accountAccess.GetRequiredUserId();

        var category = await _categories.GetByIdAsync(command.CategoryId, cancellationToken)
                       ?? throw new NotFoundException(nameof(Category), command.CategoryId);

        await _accountAccess.EnsureCanAccessEntityAsync(
            category.AccountId, nameof(Category), category.Id, cancellationToken);

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
