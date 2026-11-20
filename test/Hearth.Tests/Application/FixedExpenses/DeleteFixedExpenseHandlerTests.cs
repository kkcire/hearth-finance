using Hearth.Application.Exceptions;
using Hearth.Application.FixedExpenses.DeleteFixedExpense;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.FixedExpenses;

public class DeleteFixedExpenseHandlerTests
{
    private readonly InMemoryFixedExpenseRepository _fixedExpenses = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly DeleteFixedExpenseHandler _handler;

    public DeleteFixedExpenseHandlerTests()
    {
        _handler = new DeleteFixedExpenseHandler(_fixedExpenses, _unitOfWork);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_ExistingExpense_DeletesEvenWhenPaid(bool withPayment)
    {
        var expense = FixedExpense.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            YearMonth.Of(2026, 1),
            null,
            Money.FromDecimal(1000.00m),
            "Aluguel",
            Guid.NewGuid());

        if (withPayment)
        {
            expense.MarkAsPaid(YearMonth.Of(2026, 1), new DateOnly(2026, 1, 5));
        }

        await _fixedExpenses.AddAsync(expense);

        await _handler.ExecuteAsync(new DeleteFixedExpenseCommand(expense.Id));

        Assert.Null(await _fixedExpenses.GetByIdAsync(expense.Id));
        Assert.Equal(1, _fixedExpenses.DeleteCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownExpense_ThrowsNotFoundAndDoesNotSave()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.ExecuteAsync(new DeleteFixedExpenseCommand(unknownId)));

        Assert.Equal(nameof(FixedExpense), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
        Assert.Equal(0, _fixedExpenses.DeleteCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_NullCommand_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.ExecuteAsync(null!));
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new DeleteFixedExpenseHandler(null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new DeleteFixedExpenseHandler(_fixedExpenses, null!));
    }
}
