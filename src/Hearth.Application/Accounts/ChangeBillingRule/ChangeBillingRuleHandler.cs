using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Accounts.ChangeBillingRule;

public sealed class ChangeBillingRuleHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IAccountRepository _accounts;
    private readonly IUnitOfWork _unitOfWork;

    public ChangeBillingRuleHandler(
        AccountAccessGuard accountAccess,
        IAccountRepository accounts,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _accounts = accounts;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        ChangeBillingRuleCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var newRule = CardBillingRule.Of(command.ClosingDay, command.DueDay);

        var account = await _accountAccess.GetAuthorizedAccountAsync(command.AccountId, cancellationToken);

        account.ChangeBillingRule(newRule);

        await _accounts.UpdateAsync(account, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
