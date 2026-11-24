using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Application.PurchaseConditions;

namespace Hearth.Application.CreditCardPurchases.ListCreditCardPurchases;

public sealed class ListCreditCardPurchasesHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly ICreditCardPurchaseRepository _purchases;
    private readonly IClock _clock;

    public ListCreditCardPurchasesHandler(
        AccountAccessGuard accountAccess,
        ICreditCardPurchaseRepository purchases,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(purchases);
        ArgumentNullException.ThrowIfNull(clock);

        _accountAccess = accountAccess;
        _purchases = purchases;
        _clock = clock;
    }

    public async Task<IReadOnlyList<CreditCardPurchaseDto>> ExecuteAsync(
        ListCreditCardPurchasesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        PurchaseConditionValidation.EnsureOptionalFilterNotEmpty(query.ConditionId, nameof(query));

        _ = await _accountAccess.GetAuthorizedAccountAsync(query.AccountId, cancellationToken);

        var purchases = await _purchases.GetByAccountAsync(query.AccountId, cancellationToken);
        var today = _clock.Today;

        return purchases
            .Where(purchase => !query.ConditionId.HasValue || purchase.ConditionId == query.ConditionId.Value)
            .OrderByDescending(purchase => purchase.Date)
            .Select(purchase => CreditCardPurchaseDto.FromEntity(purchase, today))
            .ToList();
    }
}
