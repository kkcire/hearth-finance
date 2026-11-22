using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Categories;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.FixedExpenses.UpdateFixedExpenseDetails;

public sealed class UpdateFixedExpenseDetailsHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly ICategoryRepository _categories;
    private readonly IFixedExpenseRepository _fixedExpenses;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateFixedExpenseDetailsHandler(
        AccountAccessGuard accountAccess,
        ICategoryRepository categories,
        IFixedExpenseRepository fixedExpenses,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _categories = categories;
        _fixedExpenses = fixedExpenses;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        UpdateFixedExpenseDetailsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _accountAccess.GetRequiredUserId();

        var expense = await _fixedExpenses.GetByIdAsync(command.FixedExpenseId, cancellationToken)
                      ?? throw new NotFoundException(nameof(FixedExpense), command.FixedExpenseId);

        await _accountAccess.EnsureCanAccessEntityAsync(
            expense.AccountId, nameof(FixedExpense), expense.Id, cancellationToken);

        if (command.CategoryId != expense.CategoryId)
        {
            await CategoryValidation.EnsureUsableForAccountAsync(
                _categories, expense.AccountId, command.CategoryId, cancellationToken);
        }

        expense.UpdateDescription(command.Description);
        expense.UpdateCategory(command.CategoryId);

        await _fixedExpenses.UpdateAsync(expense, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
