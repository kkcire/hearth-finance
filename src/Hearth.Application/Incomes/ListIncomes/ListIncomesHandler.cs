using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;

namespace Hearth.Application.Incomes.ListIncomes;

public sealed class ListIncomesHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IIncomeRepository _incomes;

    public ListIncomesHandler(AccountAccessGuard accountAccess, IIncomeRepository incomes)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(incomes);

        _accountAccess = accountAccess;
        _incomes = incomes;
    }

    public async Task<IReadOnlyList<IncomeDto>> ExecuteAsync(
        ListIncomesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        _ = await _accountAccess.GetAuthorizedAccountAsync(query.AccountId, cancellationToken);

        var incomes = await _incomes.GetByAccountAsync(query.AccountId, cancellationToken);

        return incomes
            .OrderByDescending(income => income.ExpectedDate)
            .Select(IncomeDto.FromEntity)
            .ToList();
    }
}
