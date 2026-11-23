using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.Categories;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.CategoryBudgets.CreateCategoryBudget;

public sealed class CreateCategoryBudgetHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly ICategoryRepository _categories;
    private readonly ICategoryBudgetRepository _budgets;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCategoryBudgetHandler(
        AccountAccessGuard accountAccess,
        ICategoryRepository categories,
        ICategoryBudgetRepository budgets,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(budgets);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _categories = categories;
        _budgets = budgets;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> ExecuteAsync(
        CreateCategoryBudgetCommand command,
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

        await CategoryValidation.EnsureUsableForUserAsync(
            _categories, account.OwnerUserId, command.CategoryId, cancellationToken);

        account.EnsureCanRegisterOn(startMonth.LastDay);

        var budget = CategoryBudget.Create(
            Guid.CreateVersion7(),
            account.Id,
            command.CategoryId,
            startMonth,
            endMonth,
            amount);

        var existingBudgets = await _budgets.GetByAccountAndCategoryAsync(
            account.Id, command.CategoryId, cancellationToken);

        if (existingBudgets.Any(existing => existing.OverlapsWith(budget)))
        {
            throw new DomainException(
                "Another budget already exists for this category in an overlapping period.");
        }

        await _budgets.AddAsync(budget, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return budget.Id;
    }
}
