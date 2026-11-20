using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Application.Incomes.UpdateIncome;

public sealed class UpdateIncomeHandler
{
    private readonly IAccountRepository _accounts;
    private readonly IIncomeRepository _incomes;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateIncomeHandler(
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

    public async Task ExecuteAsync(
        UpdateIncomeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var amount = Money.FromDecimal(command.Amount);

        var income = await _incomes.GetByIdAsync(command.IncomeId, cancellationToken)
                     ?? throw new NotFoundException(nameof(Income), command.IncomeId);

        var account = await _accounts.GetByIdAsync(income.AccountId, cancellationToken)
                      ?? throw new NotFoundException(nameof(Account), income.AccountId);

        account.EnsureCanRegisterOn(command.ExpectedDate);

        income.UpdateExpectedDate(command.ExpectedDate);
        income.UpdateAmount(amount);
        income.UpdateDescription(command.Description);

        await _incomes.UpdateAsync(income, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
