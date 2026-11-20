using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.CreditCardPurchases.ListCreditCardPurchases;

public sealed class ListCreditCardPurchasesHandler
{
    private readonly IAccountRepository _accounts;
    private readonly ICreditCardPurchaseRepository _purchases;
    private readonly IClock _clock;

    public ListCreditCardPurchasesHandler(
        IAccountRepository accounts,
        ICreditCardPurchaseRepository purchases,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(purchases);
        ArgumentNullException.ThrowIfNull(clock);

        _accounts = accounts;
        _purchases = purchases;
        _clock = clock;
    }

    public async Task<IReadOnlyList<CreditCardPurchaseDto>> ExecuteAsync(
        ListCreditCardPurchasesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        _ = await _accounts.GetByIdAsync(query.AccountId, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), query.AccountId);

        var purchases = await _purchases.GetByAccountAsync(query.AccountId, cancellationToken);
        var today = _clock.Today;

        return purchases
            .OrderByDescending(purchase => purchase.Date)
            .Select(purchase => CreditCardPurchaseDto.FromEntity(purchase, today))
            .ToList();
    }
}
