using Hearth.Application.CategoryBudgets.ChangeCategoryBudgetAmount;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.CategoryBudgets;

public class ChangeCategoryBudgetAmountHandlerTests
{
    private readonly InMemoryCategoryBudgetRepository _budgets = new();
    private readonly FakeClock _clock = new(new DateOnly(2026, 11, 20));
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly ChangeCategoryBudgetAmountHandler _handler;

    public ChangeCategoryBudgetAmountHandlerTests()
    {
        _handler = new ChangeCategoryBudgetAmountHandler(_budgets, _clock, _unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_ActiveBudget_AppliesFromCurrentCalendarMonthAndSaves()
    {
        var budget = CategoryBudget.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), YearMonth.Of(2026, 1), null, Money.FromDecimal(160.00m));
        await _budgets.AddAsync(budget);

        var result = await _handler.ExecuteAsync(new ChangeCategoryBudgetAmountCommand(budget.Id, 200.00m));

        Assert.Equal(2026, result.EffectiveFromYear);
        Assert.Equal(11, result.EffectiveFromMonth);
        Assert.Equal(Money.FromDecimal(160.00m), budget.AmountIn(YearMonth.Of(2026, 10)));
        Assert.Equal(Money.FromDecimal(200.00m), budget.AmountIn(YearMonth.Of(2026, 11)));
        Assert.Equal(1, _budgets.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownBudget_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.ExecuteAsync(new ChangeCategoryBudgetAmountCommand(unknownId, 200.00m)));

        Assert.Equal(nameof(CategoryBudget), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new ChangeCategoryBudgetAmountHandler(null!, _clock, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new ChangeCategoryBudgetAmountHandler(_budgets, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new ChangeCategoryBudgetAmountHandler(_budgets, _clock, null!));
    }
}
