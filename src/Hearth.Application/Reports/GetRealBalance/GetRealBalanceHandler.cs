using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.Services;

namespace Hearth.Application.Reports.GetRealBalance;

public sealed class GetRealBalanceHandler
{
    private readonly IAccountRepository _accounts;
    private readonly IIncomeRepository _incomes;
    private readonly IDirectExpenseRepository _directExpenses;
    private readonly IFixedExpenseRepository _fixedExpenses;
    private readonly IInvoiceRepository _invoices;
    private readonly IClock _clock;

    public GetRealBalanceHandler(
        IAccountRepository accounts,
        IIncomeRepository incomes,
        IDirectExpenseRepository directExpenses,
        IFixedExpenseRepository fixedExpenses,
        IInvoiceRepository invoices,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(incomes);
        ArgumentNullException.ThrowIfNull(directExpenses);
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(invoices);
        ArgumentNullException.ThrowIfNull(clock);

        _accounts = accounts;
        _incomes = incomes;
        _directExpenses = directExpenses;
        _fixedExpenses = fixedExpenses;
        _invoices = invoices;
        _clock = clock;
    }

    public async Task<RealBalanceDto> ExecuteAsync(
        GetRealBalanceQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var account = await _accounts.GetByIdAsync(query.AccountId, cancellationToken)
                      ?? throw new NotFoundException(nameof(Account), query.AccountId);

        var until = query.Until ?? _clock.Today;

        var incomes = await _incomes.GetByAccountAsync(account.Id, cancellationToken);
        var directExpenses = await _directExpenses.GetByAccountAsync(account.Id, cancellationToken);
        var fixedExpenses = await _fixedExpenses.GetByAccountAsync(account.Id, cancellationToken);
        var invoices = await _invoices.GetByAccountAsync(account.Id, cancellationToken);

        var balance = RealBalanceCalculator.Calculate(
            account,
            until,
            incomes,
            directExpenses,
            fixedExpenses,
            invoices);

        return RealBalanceDto.FromDomain(account.Id, balance);
    }
}
