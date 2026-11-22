using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;

namespace Hearth.Application.Categories.CreateCategory;

public sealed class CreateCategoryHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCategoryHandler(
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

    public async Task<Guid> ExecuteAsync(
        CreateCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var account = await _accountAccess.GetAuthorizedAccountAsync(command.AccountId, cancellationToken);

        var category = Category.Create(Guid.CreateVersion7(), account.Id, command.Name);

        if (await _categories.ExistsWithNameAsync(account.Id, category.Name, cancellationToken: cancellationToken))
        {
            throw new DomainException($"A category named '{category.Name}' already exists in this account.");
        }

        await _categories.AddAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return category.Id;
    }
}
