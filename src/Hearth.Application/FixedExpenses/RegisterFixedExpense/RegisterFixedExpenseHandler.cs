using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.FixedExpenses.RegisterFixedExpense;

public sealed class RegisterFixedExpenseHandler
{
    private readonly IAccountRepository _accounts;
    private readonly IFixedExpenseRepository _fixedExpenses;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterFixedExpenseHandler(
        IAccountRepository accounts,
        IFixedExpenseRepository fixedExpenses,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accounts = accounts;
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

        var account = await _accounts.GetByIdAsync(command.AccountId, cancellationToken)
                      ?? throw new NotFoundException(nameof(Account), command.AccountId);

        account.EnsureCanRegisterOn(startMonth.LastDay);

        var expense = FixedExpense.Create(
            Guid.CreateVersion7(),
            account.Id,
            startMonth,
            endMonth,
            amount,
            command.Description,
            command.Category);

        await _fixedExpenses.AddAsync(expense, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return expense.Id;
    }
}
