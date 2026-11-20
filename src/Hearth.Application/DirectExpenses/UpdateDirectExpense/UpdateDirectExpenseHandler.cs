using Hearth.Application.Abstractions;
using Hearth.Application.Categories;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.DirectExpenses.UpdateDirectExpense;

public sealed class UpdateDirectExpenseHandler
{
    private readonly IAccountRepository _accounts;
    private readonly ICategoryRepository _categories;
    private readonly IDirectExpenseRepository _directExpenses;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateDirectExpenseHandler(
        IAccountRepository accounts,
        ICategoryRepository categories,
        IDirectExpenseRepository directExpenses,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(directExpenses);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accounts = accounts;
        _categories = categories;
        _directExpenses = directExpenses;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        UpdateDirectExpenseCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var amount = Money.FromDecimal(command.Amount);

        var expense = await _directExpenses.GetByIdAsync(command.DirectExpenseId, cancellationToken)
                      ?? throw new NotFoundException(nameof(DirectExpense), command.DirectExpenseId);

        var account = await _accounts.GetByIdAsync(expense.AccountId, cancellationToken)
                      ?? throw new NotFoundException(nameof(Account), expense.AccountId);

        if (command.CategoryId != expense.CategoryId)
        {
            await CategoryValidation.EnsureUsableForAccountAsync(
                _categories, account.Id, command.CategoryId, cancellationToken);
        }

        account.EnsureCanRegisterOn(command.Date);

        expense.UpdateDate(command.Date);
        expense.UpdateAmount(amount);
        expense.UpdateDescription(command.Description);
        expense.UpdateCategory(command.CategoryId);

        await _directExpenses.UpdateAsync(expense, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
