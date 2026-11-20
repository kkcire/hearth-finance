using Hearth.Application.Exceptions;
using Hearth.Application.FixedExpenses.UpdateFixedExpenseDetails;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.FixedExpenses;

public class UpdateFixedExpenseDetailsHandlerTests
{
    private readonly InMemoryFixedExpenseRepository _fixedExpenses = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly UpdateFixedExpenseDetailsHandler _handler;

    public UpdateFixedExpenseDetailsHandlerTests()
    {
        _handler = new UpdateFixedExpenseDetailsHandler(_fixedExpenses, _unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingExpense_UpdatesDescriptionAndCategoryAndSaves()
    {
        var expense = FixedExpense.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            YearMonth.Of(2026, 1),
            null,
            Money.FromDecimal(1000.00m),
            "Aluguel",
            ExpenseCategory.Services);
        await _fixedExpenses.AddAsync(expense);

        await _handler.ExecuteAsync(new UpdateFixedExpenseDetailsCommand(
            expense.Id, "  Faculdade  ", ExpenseCategory.Education));

        Assert.Equal("Faculdade", expense.Description);
        Assert.Equal(ExpenseCategory.Education, expense.Category);
        Assert.Equal(1, _fixedExpenses.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownExpense_ThrowsNotFoundAndDoesNotSave()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new UpdateFixedExpenseDetailsCommand(unknownId, "Internet", ExpenseCategory.Services)));

        Assert.Equal(nameof(FixedExpense), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
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
        Assert.Throws<ArgumentNullException>(() => new UpdateFixedExpenseDetailsHandler(null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new UpdateFixedExpenseDetailsHandler(_fixedExpenses, null!));
    }
}
