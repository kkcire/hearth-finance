using Hearth.Application.Abstractions;
using Hearth.Application.Categories;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.DirectExpenses.RegisterDirectExpense;

public sealed class RegisterDirectExpenseHandler
{
    private readonly IAccountRepository _accounts;
    private readonly ICategoryRepository _categories;
    private readonly IDirectExpenseRepository _directExpenses;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterDirectExpenseHandler(
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

    public async Task<Guid> ExecuteAsync(
        RegisterDirectExpenseCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var amount = Money.FromDecimal(command.Amount);

        var account = await _accounts.GetByIdAsync(command.AccountId, cancellationToken)
                      ?? throw new NotFoundException(nameof(Account), command.AccountId);

        await CategoryValidation.EnsureUsableForAccountAsync(
            _categories, account.Id, command.CategoryId, cancellationToken);

        account.EnsureCanRegisterOn(command.Date);

        var expense = DirectExpense.Create(
            Guid.CreateVersion7(),
            account.Id,
            command.Date,
            amount,
            command.Description,
            command.CategoryId);

        await _directExpenses.AddAsync(expense, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return expense.Id;
    }
}
