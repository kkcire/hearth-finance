using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Incomes.RegisterIncome;

public sealed class RegisterIncomeHandler
{
    private readonly IAccountRepository _accounts;
    private readonly IIncomeRepository _incomes;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterIncomeHandler(
        IAccountRepository accounts,
        IIncomeRepository incomes,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(incomes);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _accounts = accounts;
        _incomes = incomes;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> ExecuteAsync(
        RegisterIncomeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var amount = Money.FromDecimal(command.Amount);

        var account = await _accounts.GetByIdAsync(command.AccountId, cancellationToken)
                      ?? throw new NotFoundException(nameof(Account), command.AccountId);

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
            income.MarkAsReceived(received);
        }

        await _incomes.AddAsync(income, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return income.Id;
    }
}
