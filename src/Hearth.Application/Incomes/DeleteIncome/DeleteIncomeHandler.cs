using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.Incomes.DeleteIncome;

public sealed class DeleteIncomeHandler
{
    private readonly IIncomeRepository _incomes;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteIncomeHandler(IIncomeRepository incomes, IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(incomes);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _incomes = incomes;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        DeleteIncomeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var income = await _incomes.GetByIdAsync(command.IncomeId, cancellationToken)
                     ?? throw new NotFoundException(nameof(Income), command.IncomeId);

        await _incomes.DeleteAsync(income, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
