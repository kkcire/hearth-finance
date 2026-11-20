using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.FixedExpenses.EndFixedExpense;

public sealed class EndFixedExpenseHandler
{
    private readonly IAccountRepository _accounts;
    private readonly IFixedExpenseRepository _fixedExpenses;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public EndFixedExpenseHandler(
        IAccountRepository accounts,
        IFixedExpenseRepository fixedExpenses,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accounts = accounts;
        _fixedExpenses = fixedExpenses;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        EndFixedExpenseCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var lastMonth = YearMonth.Of(command.LastYear, command.LastMonth);

        var expense = await _fixedExpenses.GetByIdAsync(command.FixedExpenseId, cancellationToken)
                      ?? throw new NotFoundException(nameof(FixedExpense), command.FixedExpenseId);

        var account = await _accounts.GetByIdAsync(expense.AccountId, cancellationToken)
                      ?? throw new NotFoundException(nameof(Account), expense.AccountId);

        expense.End(lastMonth, _clock.Today, account.BillingRule);

        await _fixedExpenses.UpdateAsync(expense, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
