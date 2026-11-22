using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Domain.Services;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Reports.GetProjectedResult;

public sealed class GetProjectedResultHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IIncomeRepository _incomes;
    private readonly IDirectExpenseRepository _directExpenses;
    private readonly IFixedExpenseRepository _fixedExpenses;
    private readonly ICreditCardPurchaseRepository _purchases;

    public GetProjectedResultHandler(
        AccountAccessGuard accountAccess,
        IIncomeRepository incomes,
        IDirectExpenseRepository directExpenses,
        IFixedExpenseRepository fixedExpenses,
        ICreditCardPurchaseRepository purchases)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(incomes);
        ArgumentNullException.ThrowIfNull(directExpenses);
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(purchases);

        _accountAccess = accountAccess;
        _incomes = incomes;
        _directExpenses = directExpenses;
        _fixedExpenses = fixedExpenses;
        _purchases = purchases;
    }

    public async Task<ProjectedResultDto> ExecuteAsync(
        GetProjectedResultQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var month = YearMonth.Of(query.Year, query.Month);

        var account = await _accountAccess.GetAuthorizedAccountAsync(query.AccountId, cancellationToken);

        var incomes = await _incomes.GetByAccountAsync(account.Id, cancellationToken);
        var directExpenses = await _directExpenses.GetByAccountAsync(account.Id, cancellationToken);
        var fixedExpenses = await _fixedExpenses.GetByAccountAsync(account.Id, cancellationToken);
        var purchases = await _purchases.GetByAccountAsync(account.Id, cancellationToken);

        var result = ProjectedResultCalculator.Calculate(
            account.Id,
            month,
            incomes,
            directExpenses,
            fixedExpenses,
            purchases);

        return ProjectedResultDto.FromDomain(account.Id, result);
    }
}
