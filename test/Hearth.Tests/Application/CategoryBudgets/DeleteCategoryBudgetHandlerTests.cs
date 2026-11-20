using Hearth.Application.CategoryBudgets.DeleteCategoryBudget;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.CategoryBudgets;

public class DeleteCategoryBudgetHandlerTests
{
    private readonly InMemoryCategoryBudgetRepository _budgets = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly DeleteCategoryBudgetHandler _handler;

    public DeleteCategoryBudgetHandlerTests()
    {
        _handler = new DeleteCategoryBudgetHandler(_budgets, _unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingBudget_DeletesAndSaves()
    {
        var budget = CategoryBudget.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), YearMonth.Of(2026, 1), null, Money.FromDecimal(160.00m));
        await _budgets.AddAsync(budget);

        await _handler.ExecuteAsync(new DeleteCategoryBudgetCommand(budget.Id));

        Assert.Null(await _budgets.GetByIdAsync(budget.Id));
        Assert.Equal(1, _budgets.DeleteCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownBudget_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.ExecuteAsync(new DeleteCategoryBudgetCommand(unknownId)));

        Assert.Equal(nameof(CategoryBudget), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new DeleteCategoryBudgetHandler(null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new DeleteCategoryBudgetHandler(_budgets, null!));
    }
}
