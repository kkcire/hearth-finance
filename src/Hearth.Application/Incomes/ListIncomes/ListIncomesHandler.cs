using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;

namespace Hearth.Application.Incomes.ListIncomes;

public sealed class ListIncomesHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IIncomeRepository _incomes;
    private readonly IClock _clock;

    public ListIncomesHandler(
        AccountAccessGuard accountAccess,
        IIncomeRepository incomes,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(incomes);
        ArgumentNullException.ThrowIfNull(clock);

        _accountAccess = accountAccess;
        _incomes = incomes;
        _clock = clock;
    }

    public async Task<IReadOnlyList<IncomeDto>> ExecuteAsync(
        ListIncomesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        _ = await _accountAccess.GetAuthorizedAccountAsync(query.AccountId, cancellationToken);

        var incomes = await _incomes.GetByAccountAsync(query.AccountId, cancellationToken);
        var today = _clock.Today;

        return incomes
            .OrderByDescending(income => income.Date)
            .Select(income => IncomeDto.FromEntity(income, today))
            .ToList();
    }
}
