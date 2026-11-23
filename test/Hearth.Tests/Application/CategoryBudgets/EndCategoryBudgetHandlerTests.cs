using Hearth.Application.Authorization;
using Hearth.Application.CategoryBudgets.EndCategoryBudget;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.CategoryBudgets;

public class EndCategoryBudgetHandlerTests
{
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryCategoryBudgetRepository _budgets = new();
    private readonly FakeClock _clock = new(new DateOnly(2026, 10, 15));
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly EndCategoryBudgetHandler _handler;

    public EndCategoryBudgetHandlerTests()
    {
        _accounts.AddAsync(Account.Create(
            _accountId,
            FakeCurrentUser.DefaultUserId,
            "Conta",
            Money.Zero,
            new DateOnly(2026, 1, 1))).GetAwaiter().GetResult();
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new EndCategoryBudgetHandler(_accountAccess, _budgets, _clock, _unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_ValidEndMonth_UpdatesAndSaves()
    {
        var budget = CategoryBudget.Create(
            Guid.NewGuid(), _accountId, Guid.NewGuid(), YearMonth.Of(2026, 1), null, Money.FromDecimal(160.00m));
        await _budgets.AddAsync(budget);

        await _handler.ExecuteAsync(new EndCategoryBudgetCommand(budget.Id, 2026, 10));

        Assert.Equal(YearMonth.Of(2026, 10), budget.EndMonth);
        Assert.Equal(1, _budgets.UpdateCount);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_ExtendingIntoAnotherBudgetOfSameCategory_ThrowsDomainException()
    {
        var accountId = _accountId;
        var categoryId = Guid.NewGuid();

        var first = CategoryBudget.Create(
            Guid.NewGuid(), accountId, categoryId, YearMonth.Of(2026, 1), YearMonth.Of(2026, 10), Money.FromDecimal(160.00m));
        var second = CategoryBudget.Create(
            Guid.NewGuid(), accountId, categoryId, YearMonth.Of(2026, 12), null, Money.FromDecimal(200.00m));
        await _budgets.AddAsync(first);
        await _budgets.AddAsync(second);

        await Assert.ThrowsAsync<DomainException>(() =>
            _handler.ExecuteAsync(new EndCategoryBudgetCommand(first.Id, 2026, 12)));

        Assert.Equal(YearMonth.Of(2026, 10), first.EndMonth);
        Assert.Equal(0, _budgets.UpdateCount);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownBudget_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.ExecuteAsync(new EndCategoryBudgetCommand(unknownId, 2026, 12)));

        Assert.Equal(nameof(CategoryBudget), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new EndCategoryBudgetHandler(null!, _budgets, _clock, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new EndCategoryBudgetHandler(_accountAccess, null!, _clock, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new EndCategoryBudgetHandler(_accountAccess, _budgets, null!, _unitOfWork));
        Assert.Throws<ArgumentNullException>(() => new EndCategoryBudgetHandler(_accountAccess, _budgets, _clock, null!));
    }
}
