using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.FixedIncomes.ListFixedIncomes;

public sealed class ListFixedIncomesHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IFixedIncomeRepository _fixedIncomes;
    private readonly IPurchaseConditionRepository _conditions;
    private readonly IClock _clock;

    public ListFixedIncomesHandler(
        AccountAccessGuard accountAccess,
        IFixedIncomeRepository fixedIncomes,
        IPurchaseConditionRepository conditions,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(fixedIncomes);
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(clock);

        _accountAccess = accountAccess;
        _fixedIncomes = fixedIncomes;
        _conditions = conditions;
        _clock = clock;
    }

    public async Task<IReadOnlyList<FixedIncomeDto>> ExecuteAsync(
        ListFixedIncomesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Year.HasValue != query.Month.HasValue)
        {
            throw new ArgumentException("Both Year and Month must be provided together.", nameof(query));
        }

        var targetMonth = query.Year.HasValue
            ? YearMonth.Of(query.Year.Value, query.Month!.Value)
            : YearMonth.From(_clock.Today);

        var account = await _accountAccess.GetAuthorizedAccountAsync(query.AccountId, cancellationToken);
        var conditions = await _conditions.GetVisibleForUserAsync(account.OwnerUserId, cancellationToken);
        var conditionNames = conditions.ToDictionary(c => c.Id, c => c.Name);
        var incomes = await _fixedIncomes.GetByAccountAsync(account.Id, cancellationToken);

        return incomes
            .OrderBy(income => income.ExpectedDayOfMonth)
            .ThenBy(income => income.Description, StringComparer.OrdinalIgnoreCase)
            .Select(income => FixedIncomeDto.FromEntity(
                income,
                targetMonth,
                conditionNames.GetValueOrDefault(income.ConditionId, "Própria")))
            .ToList();
    }
}
