using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.PurchaseConditions;
using Hearth.Domain.Services;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Reports.GetMonthlySurplus;

public sealed class GetMonthlySurplusHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IIncomeRepository _incomes;
    private readonly IDirectExpenseRepository _directExpenses;
    private readonly IFixedExpenseRepository _fixedExpenses;
    private readonly ICreditCardPurchaseRepository _purchases;
    private readonly IClock _clock;

    public GetMonthlySurplusHandler(
        AccountAccessGuard accountAccess,
        IIncomeRepository incomes,
        IDirectExpenseRepository directExpenses,
        IFixedExpenseRepository fixedExpenses,
        ICreditCardPurchaseRepository purchases,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(incomes);
        ArgumentNullException.ThrowIfNull(directExpenses);
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(purchases);
        ArgumentNullException.ThrowIfNull(clock);

        _accountAccess = accountAccess;
        _incomes = incomes;
        _directExpenses = directExpenses;
        _fixedExpenses = fixedExpenses;
        _purchases = purchases;
        _clock = clock;
    }

    public async Task<MonthlySurplusDto> ExecuteAsync(
        GetMonthlySurplusQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        PurchaseConditionValidation.EnsureOptionalFilterNotEmpty(query.ConditionId, nameof(query));

        var selectedMonth = YearMonth.Of(query.Year, query.Month);
        var account = await _accountAccess.GetAuthorizedAccountAsync(query.AccountId, cancellationToken);

        var incomes = await _incomes.GetByAccountAsync(account.Id, cancellationToken);
        var directExpenses = await _directExpenses.GetByAccountAsync(account.Id, cancellationToken);
        var fixedExpenses = await _fixedExpenses.GetByAccountAsync(account.Id, cancellationToken);
        var purchases = await _purchases.GetByAccountAsync(account.Id, cancellationToken);

        var series = MonthlySurplusCalculator.CalculateSeries(
            account,
            selectedMonth,
            _clock.Today,
            incomes,
            directExpenses,
            fixedExpenses,
            purchases,
            query.PastMonths,
            query.FutureMonths,
            query.ConditionId);

        return MonthlySurplusDto.FromDomain(account.Id, series);
    }
}
