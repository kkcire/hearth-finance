using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Accounts.CreateAccount;

public sealed class CreateAccountHandler
{
    private readonly IAccountRepository _accounts;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAccountHandler(IAccountRepository accounts, IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accounts = accounts;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> ExecuteAsync(
        CreateAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var initialBalance = Money.FromDecimal(command.InitialBalance);
        var billingRule = CardBillingRule.Of(command.ClosingDay, command.DueDay);

        var account = Account.Create(
            Guid.CreateVersion7(),
            command.Name,
            initialBalance,
            command.InitialBalanceDate,
            billingRule);

        await _accounts.AddAsync(account, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return account.Id;
    }
}
