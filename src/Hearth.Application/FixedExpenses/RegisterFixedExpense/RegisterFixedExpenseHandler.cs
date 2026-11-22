using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Categories;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.FixedExpenses.RegisterFixedExpense;

public sealed class RegisterFixedExpenseHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly ICategoryRepository _categories;
    private readonly IFixedExpenseRepository _fixedExpenses;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterFixedExpenseHandler(
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

    public async Task<Guid> ExecuteAsync(
        RegisterFixedExpenseCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.EndYear.HasValue != command.EndMonth.HasValue)
        {
            throw new ArgumentException(
                "Both end year and end month must be provided together.", nameof(command));
        }

        var startMonth = YearMonth.Of(command.StartYear, command.StartMonth);
        YearMonth? endMonth = command.EndYear.HasValue
            ? YearMonth.Of(command.EndYear.Value, command.EndMonth!.Value)
            : null;
        var amount = Money.FromDecimal(command.Amount);

        var account = await _accountAccess.GetAuthorizedAccountAsync(command.AccountId, cancellationToken);

        await CategoryValidation.EnsureUsableForAccountAsync(
            _categories, account.Id, command.CategoryId, cancellationToken);

        account.EnsureCanRegisterOn(startMonth.LastDay);

        var expense = FixedExpense.Create(
            Guid.CreateVersion7(),
            account.Id,
            startMonth,
            endMonth,
            amount,
            command.Description,
            command.CategoryId);

        await _fixedExpenses.AddAsync(expense, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return expense.Id;
    }
}
