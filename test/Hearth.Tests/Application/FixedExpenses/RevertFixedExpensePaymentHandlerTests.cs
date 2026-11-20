using Hearth.Application.Exceptions;
using Hearth.Application.FixedExpenses.RevertFixedExpensePayment;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.FixedExpenses;

public class RevertFixedExpensePaymentHandlerTests
{
    private readonly InMemoryFixedExpenseRepository _fixedExpenses = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RevertFixedExpensePaymentHandler _handler;

    public RevertFixedExpensePaymentHandlerTests()
    {
        _handler = new RevertFixedExpensePaymentHandler(_fixedExpenses, _unitOfWork);
    }

    private async Task<FixedExpense> SeedAsync(bool paid)
    {
        var expense = FixedExpense.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            YearMonth.Of(2026, 10),
            null,
            Money.FromDecimal(1000.00m),
            "Aluguel",
            Guid.NewGuid());

        if (paid)
        {
            expense.MarkAsPaid(YearMonth.Of(2026, 10), new DateOnly(2026, 10, 5));
        }

        await _fixedExpenses.AddAsync(expense);
        return expense;
    }

    [Fact]
    public async Task ExecuteAsync_PaidMonth_RevertsAndSaves()
    {
        var expense = await SeedAsync(paid: true);

        var changed = await _handler.ExecuteAsync(
            new RevertFixedExpensePaymentCommand(expense.Id, 2026, 10));

        Assert.True(changed);
        Assert.False(expense.IsPaid(YearMonth.Of(2026, 10)));
        Assert.Equal(1, _fixedExpenses.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnpaidMonth_IsIdempotentAndDoesNotSave()
    {
        var expense = await SeedAsync(paid: false);

        var changed = await _handler.ExecuteAsync(
            new RevertFixedExpensePaymentCommand(expense.Id, 2026, 10));

        Assert.False(changed);
        Assert.Equal(0, _fixedExpenses.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownExpense_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _handler.ExecuteAsync(
            new RevertFixedExpensePaymentCommand(unknownId, 2026, 10)));

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
        Assert.Throws<ArgumentNullException>(() => new RevertFixedExpensePaymentHandler(null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new RevertFixedExpensePaymentHandler(_fixedExpenses, null!));
    }
}
