using Hearth.Application.Authorization;
using Hearth.Application.CategoryBudgets.ListCategoryBudgets;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Xunit;

namespace Hearth.Tests.Application.CategoryBudgets;

public class ListCategoryBudgetsHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly InMemoryCategoryBudgetRepository _budgets = new();
    private readonly ListCategoryBudgetsHandler _handler;

    public ListCategoryBudgetsHandlerTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
        _handler = new ListCategoryBudgetsHandler(_accountAccess, _categories, _budgets);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsAccountBudgetsWithCategoryNamesAndHistory()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId, "Minha", Money.Zero, TestTime.UtcOn(new DateOnly(2026, 1, 1)));
        await _accounts.AddAsync(account);

        var mobility = Category.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Mobilidade");
        await _categories.AddAsync(mobility);

        var budget = CategoryBudget.Create(
            Guid.NewGuid(), account.Id, mobility.Id, YearMonth.Of(2026, 1), null, Money.FromDecimal(160.00m));
        budget.ChangeAmount(Money.FromDecimal(200.00m), new DateOnly(2026, 11, 10));
        await _budgets.AddAsync(budget);

        var result = await _handler.ExecuteAsync(new ListCategoryBudgetsQuery(account.Id));

        var dto = Assert.Single(result);
        Assert.Equal(budget.Id, dto.Id);
        Assert.Equal(mobility.Id, dto.CategoryId);
        Assert.Equal("Mobilidade", dto.CategoryName);
        Assert.Equal(2, dto.AmountHistory.Count);
        Assert.Equal(160.00m, dto.AmountHistory[0].Amount);
        Assert.Equal(200.00m, dto.AmountHistory[1].Amount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.ExecuteAsync(new ListCategoryBudgetsQuery(unknownId)));

        Assert.Equal(nameof(Account), exception.ResourceName);
        Assert.Equal(unknownId, exception.ResourceId);
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new ListCategoryBudgetsHandler(null!, _categories, _budgets));
        Assert.Throws<ArgumentNullException>(() => new ListCategoryBudgetsHandler(_accountAccess, null!, _budgets));
        Assert.Throws<ArgumentNullException>(() => new ListCategoryBudgetsHandler(_accountAccess, _categories, null!));
    }
}
