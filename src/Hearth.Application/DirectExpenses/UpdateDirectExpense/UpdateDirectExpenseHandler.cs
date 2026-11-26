using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Categories;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.DirectExpenses.UpdateDirectExpense;

public sealed class UpdateDirectExpenseHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly ICategoryRepository _categories;
    private readonly IDirectExpenseRepository _directExpenses;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateDirectExpenseHandler(
        AccountAccessGuard accountAccess,
        ICategoryRepository categories,
        IDirectExpenseRepository directExpenses,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(directExpenses);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _categories = categories;
        _directExpenses = directExpenses;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        UpdateDirectExpenseCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _accountAccess.GetRequiredUserId();
        var amount = Money.FromDecimal(command.Amount);

        var expense = await _directExpenses.GetByIdAsync(command.DirectExpenseId, cancellationToken)
                      ?? throw new NotFoundException(nameof(DirectExpense), command.DirectExpenseId);

        var account = await _accountAccess.EnsureCanAccessEntityAsync(
            expense.AccountId, nameof(DirectExpense), expense.Id, cancellationToken);

        if (command.CategoryId != expense.CategoryId)
        {
            await CategoryValidation.EnsureUsableForUserAsync(
                _categories, account.OwnerUserId, command.CategoryId, cancellationToken);
        }

        expense.UpdateDate(account, command.Date);
        expense.UpdateAmount(amount);
        expense.UpdateDescription(command.Description);
        expense.UpdateCategory(command.CategoryId);

        await _directExpenses.UpdateAsync(expense, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
