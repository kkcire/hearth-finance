using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Incomes.RegisterIncome;

public sealed class RegisterIncomeHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IIncomeRepository _incomes;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterIncomeHandler(
        AccountAccessGuard accountAccess,
        IIncomeRepository incomes,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(incomes);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accountAccess = accountAccess;
        _incomes = incomes;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> ExecuteAsync(
        RegisterIncomeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var amount = Money.FromDecimal(command.Amount);

        var account = await _accountAccess.GetAuthorizedAccountAsync(command.AccountId, cancellationToken);

        account.EnsureCanRegisterOn(command.ExpectedDate);

        if (command.ReceivedOn is { } receivedOn)
        {
            account.EnsureCanRegisterOn(receivedOn);
        }

        var income = Income.Create(
            Guid.CreateVersion7(),
            account.Id,
            command.ExpectedDate,
            amount,
            command.Description);

        if (command.ReceivedOn is { } received)
        {
            income.MarkAsReceived(received, _clock.Today);
        }

        await _incomes.AddAsync(income, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return income.Id;
    }
}
