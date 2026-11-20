using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.Incomes.RevertIncomeToExpected;

public sealed class RevertIncomeToExpectedHandler
{
    private readonly IIncomeRepository _incomes;
    private readonly IUnitOfWork _unitOfWork;

    public RevertIncomeToExpectedHandler(IIncomeRepository incomes, IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(incomes);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _incomes = incomes;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> ExecuteAsync(
        RevertIncomeToExpectedCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var income = await _incomes.GetByIdAsync(command.IncomeId, cancellationToken)
                     ?? throw new NotFoundException(nameof(Income), command.IncomeId);

        var changed = income.RevertToExpected();

        if (changed)
        {
            await _incomes.UpdateAsync(income, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }
}
