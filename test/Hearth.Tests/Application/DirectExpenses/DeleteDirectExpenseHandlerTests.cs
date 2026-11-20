using Hearth.Application.DirectExpenses.DeleteDirectExpense;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.DirectExpenses;

public class DeleteDirectExpenseHandlerTests
{
    private readonly InMemoryDirectExpenseRepository _directExpenses = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly DeleteDirectExpenseHandler _handler;

    public DeleteDirectExpenseHandlerTests()
    {
        _handler = new DeleteDirectExpenseHandler(_directExpenses, _unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingExpense_DeletesAndSaves()
    {
        var expense = DirectExpense.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(50.00m),
            "Mercado",
            Guid.NewGuid());
        await _directExpenses.AddAsync(expense);

        await _handler.ExecuteAsync(new DeleteDirectExpenseCommand(expense.Id));

        Assert.Null(await _directExpenses.GetByIdAsync(expense.Id));
        Assert.Equal(1, _directExpenses.DeleteCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownExpense_ThrowsNotFoundAndDoesNotSave()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.ExecuteAsync(new DeleteDirectExpenseCommand(unknownId)));

        Assert.Equal(nameof(DirectExpense), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
        Assert.Equal(0, _directExpenses.DeleteCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsCancellationToken()
    {
        var expense = DirectExpense.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 10, 10),
            Money.FromDecimal(50.00m),
            "Mercado",
            Guid.NewGuid());
        await _directExpenses.AddAsync(expense);
        using var cts = new CancellationTokenSource();

        await _handler.ExecuteAsync(new DeleteDirectExpenseCommand(expense.Id), cts.Token);

        Assert.Equal(cts.Token, _directExpenses.LastCancellationToken);
        Assert.Equal(cts.Token, _unitOfWork.LastCancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_NullCommand_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.ExecuteAsync(null!));
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new DeleteDirectExpenseHandler(null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new DeleteDirectExpenseHandler(_directExpenses, null!));
    }
}
