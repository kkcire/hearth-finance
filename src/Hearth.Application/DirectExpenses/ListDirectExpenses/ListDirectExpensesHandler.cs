using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;

namespace Hearth.Application.DirectExpenses.ListDirectExpenses;

public sealed class ListDirectExpensesHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IDirectExpenseRepository _directExpenses;

    public ListDirectExpensesHandler(
        AccountAccessGuard accountAccess,
        IDirectExpenseRepository directExpenses)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(directExpenses);

        _accountAccess = accountAccess;
        _directExpenses = directExpenses;
    }

    public async Task<IReadOnlyList<DirectExpenseDto>> ExecuteAsync(
        ListDirectExpensesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        _ = await _accountAccess.GetAuthorizedAccountAsync(query.AccountId, cancellationToken);

        var expenses = await _directExpenses.GetByAccountAsync(query.AccountId, cancellationToken);

        return expenses
            .OrderByDescending(expense => expense.Date)
            .Select(DirectExpenseDto.FromEntity)
            .ToList();
    }
}
