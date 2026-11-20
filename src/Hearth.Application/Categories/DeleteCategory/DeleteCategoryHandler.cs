using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;

namespace Hearth.Application.Categories.DeleteCategory;

public sealed class DeleteCategoryHandler
{
    private readonly ICategoryRepository _categories;
    private readonly IDirectExpenseRepository _directExpenses;
    private readonly IFixedExpenseRepository _fixedExpenses;
    private readonly ICreditCardPurchaseRepository _purchases;
    private readonly ICategoryBudgetRepository _budgets;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCategoryHandler(
        ICategoryRepository categories,
        IDirectExpenseRepository directExpenses,
        IFixedExpenseRepository fixedExpenses,
        ICreditCardPurchaseRepository purchases,
        ICategoryBudgetRepository budgets,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(directExpenses);
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(purchases);
        ArgumentNullException.ThrowIfNull(budgets);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _categories = categories;
        _directExpenses = directExpenses;
        _fixedExpenses = fixedExpenses;
        _purchases = purchases;
        _budgets = budgets;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        DeleteCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await _categories.GetByIdAsync(command.CategoryId, cancellationToken)
                       ?? throw new NotFoundException(nameof(Category), command.CategoryId);

        if (await _directExpenses.AnyByCategoryAsync(category.Id, cancellationToken)
            || await _fixedExpenses.AnyByCategoryAsync(category.Id, cancellationToken)
            || await _purchases.AnyByCategoryAsync(category.Id, cancellationToken)
            || await _budgets.AnyByCategoryAsync(category.Id, cancellationToken))
        {
            throw new DomainException(
                "A category that is used by expenses or budgets cannot be deleted. Archive it instead.");
        }

        await _categories.DeleteAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
