using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.Incomes.ListIncomes;

public sealed class ListIncomesHandler
{
    private readonly IAccountRepository _accounts;
    private readonly IIncomeRepository _incomes;

    public ListIncomesHandler(IAccountRepository accounts, IIncomeRepository incomes)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(incomes);

        _accounts = accounts;
        _incomes = incomes;
    }

    public async Task<IReadOnlyList<IncomeDto>> ExecuteAsync(
        ListIncomesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        _ = await _accounts.GetByIdAsync(query.AccountId, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), query.AccountId);

        var incomes = await _incomes.GetByAccountAsync(query.AccountId, cancellationToken);

        return incomes
            .OrderByDescending(income => income.ExpectedDate)
            .Select(IncomeDto.FromEntity)
            .ToList();
    }
}
