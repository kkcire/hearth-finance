using Hearth.Application;
using Hearth.Application.Authorization;
using Hearth.Application.Exceptions;
using Hearth.Application.FixedIncomes.ConfirmFixedIncomeReceipt;
using Hearth.Application.FixedIncomes.CreateFixedIncome;
using Hearth.Application.FixedIncomes.DeleteFixedIncome;
using Hearth.Application.FixedIncomes.ListFixedIncomes;
using Hearth.Application.FixedIncomes.RevertFixedIncomeReceipt;
using Hearth.Application.FixedIncomes.StopFixedIncome;
using Hearth.Application.FixedIncomes.UpdateFixedIncome;
using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.Exceptions;
using Hearth.Domain.ValueObjects;
using Hearth.Tests.Application.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hearth.Tests.Application.FixedIncomes;

public sealed class FixedIncomeHandlersTests
{
    private static readonly DateOnly InitialDate = new(2026, 10, 1);

    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AccountAccessGuard _accountAccess;
    private readonly InMemoryPurchaseConditionRepository _conditions = new();
    private readonly InMemoryFixedIncomeRepository _fixedIncomes = new();
    private readonly FakeClock _clock = new(new DateOnly(2026, 11, 20));
    private readonly FakeUnitOfWork _unitOfWork = new();

    public FixedIncomeHandlersTests()
    {
        _accountAccess = new AccountAccessGuard(_accounts, _currentUser);
    }

    private async Task<Account> SeedAccountAsync()
    {
        var account = Account.Create(
            Guid.NewGuid(),
            FakeCurrentUser.DefaultUserId,
            "Conta Principal",
            Money.FromDecimal(1000m),
            TestTime.UtcOn(InitialDate));
        await _accounts.AddAsync(account);
        return account;
    }

    [Fact]
    public async Task CreateUpdateListConfirmRevertStopAndDelete_FullLifecycleWorksEndToEnd()
    {
        var account = await SeedAccountAsync();
        var pai = PurchaseCondition.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Pai");
        await _conditions.AddAsync(pai);

        // 1. Create
        var createHandler = new CreateFixedIncomeHandler(_accountAccess, _fixedIncomes, _conditions, _unitOfWork);
        var id = await createHandler.ExecuteAsync(new CreateFixedIncomeCommand(
            account.Id, 2026, 10, null, null, 5, 5000m, "  Salário  "));

        Assert.NotEqual(Guid.Empty, id);

        // 2. Update details + salary adjustment starting Nov 2026
        var updateHandler = new UpdateFixedIncomeHandler(_accountAccess, _fixedIncomes, _conditions, _clock, _unitOfWork);
        await updateHandler.ExecuteAsync(new UpdateFixedIncomeCommand(
            id, "Salário CLT", 6, SystemPurchaseConditions.OwnId, NewAmount: 5500m, EffectiveFromYear: 2026, EffectiveFromMonth: 11));

        var listHandler = new ListFixedIncomesHandler(_accountAccess, _fixedIncomes, _conditions, _clock);

        // Oct still expects 5000 (history preserved), unconfirmed
        var octList = await listHandler.ExecuteAsync(new ListFixedIncomesQuery(account.Id, 2026, 10));
        var octItem = Assert.Single(octList);
        Assert.Equal("Salário CLT", octItem.Description);
        Assert.Equal(6, octItem.ExpectedDayOfMonth);
        Assert.Equal("Própria", octItem.ConditionName);
        Assert.True(octItem.IsOwn);
        Assert.True(octItem.IsActiveForMonth);
        Assert.Equal(5000m, octItem.ExpectedAmount);
        Assert.False(octItem.IsReceivedForMonth);
        Assert.Null(octItem.ReceivedOn);
        Assert.Null(octItem.ActualAmount);

        // 3. Confirm receipt in Nov with R$ 350 bonus (5850 vs 5500 expected)
        var confirmHandler = new ConfirmFixedIncomeReceiptHandler(_accountAccess, _fixedIncomes, _clock, _unitOfWork);
        var receipt = await confirmHandler.ExecuteAsync(new ConfirmFixedIncomeReceiptCommand(
            id, 2026, 11, new DateOnly(2026, 11, 6), 5850m));
        Assert.Equal(5850m, receipt.ActualAmount);
        Assert.Equal(new DateOnly(2026, 11, 6), receipt.ReceivedOn);

        // Duplicate month confirmation throws
        await Assert.ThrowsAsync<DomainException>(() => confirmHandler.ExecuteAsync(
            new ConfirmFixedIncomeReceiptCommand(id, 2026, 11, new DateOnly(2026, 11, 7), 5500m)));

        // List for Nov reflects receipt actual amount + date
        var novList = await listHandler.ExecuteAsync(new ListFixedIncomesQuery(account.Id, 2026, 11));
        var novItem = Assert.Single(novList);
        Assert.Equal(5500m, novItem.ExpectedAmount);
        Assert.True(novItem.IsReceivedForMonth);
        Assert.Equal(new DateOnly(2026, 11, 6), novItem.ReceivedOn);
        Assert.Equal(5850m, novItem.ActualAmount);

        // 4. Stop at Nov (protected against stopping before Nov confirmed receipt)
        var stopHandler = new StopFixedIncomeHandler(_accountAccess, _fixedIncomes, _unitOfWork);
        await Assert.ThrowsAsync<DomainException>(() => stopHandler.ExecuteAsync(
            new StopFixedIncomeCommand(id, 2026, 10)));

        await stopHandler.ExecuteAsync(new StopFixedIncomeCommand(id, 2026, 11));
        var decStopped = Assert.Single(await listHandler.ExecuteAsync(new ListFixedIncomesQuery(account.Id, 2026, 12)));
        Assert.False(decStopped.IsActiveForMonth);
        Assert.Null(decStopped.ExpectedAmount);

        // Reactivate (EndYear/EndMonth null)
        await stopHandler.ExecuteAsync(new StopFixedIncomeCommand(id, null, null));
        var decActive = Assert.Single(await listHandler.ExecuteAsync(new ListFixedIncomesQuery(account.Id, 2026, 12)));
        Assert.True(decActive.IsActiveForMonth);
        Assert.Equal(5500m, decActive.ExpectedAmount);

        // 5. Revert Nov receipt
        var revertHandler = new RevertFixedIncomeReceiptHandler(_accountAccess, _fixedIncomes, _unitOfWork);
        Assert.True(await revertHandler.ExecuteAsync(new RevertFixedIncomeReceiptCommand(id, 2026, 11)));
        Assert.False(await revertHandler.ExecuteAsync(new RevertFixedIncomeReceiptCommand(id, 2026, 11)));

        var novAfterRevert = Assert.Single(await listHandler.ExecuteAsync(new ListFixedIncomesQuery(account.Id, 2026, 11)));
        Assert.False(novAfterRevert.IsReceivedForMonth);
        Assert.Null(novAfterRevert.ActualAmount);

        // 6. Custom condition reflected in ConditionName / IsOwn
        await updateHandler.ExecuteAsync(new UpdateFixedIncomeCommand(id, "Repasse Pai", 10, pai.Id));
        var withCustomCondition = Assert.Single(await listHandler.ExecuteAsync(new ListFixedIncomesQuery(account.Id, 2026, 11)));
        Assert.Equal("Pai", withCustomCondition.ConditionName);
        Assert.False(withCustomCondition.IsOwn);

        // 7. Delete
        var deleteHandler = new DeleteFixedIncomeHandler(_accountAccess, _fixedIncomes, _unitOfWork);
        await deleteHandler.ExecuteAsync(new DeleteFixedIncomeCommand(id));
        Assert.Empty(await listHandler.ExecuteAsync(new ListFixedIncomesQuery(account.Id, 2026, 11)));
    }

    [Fact]
    public async Task Create_RejectsArchivedConditionAndPreTrackingStartMonth()
    {
        var account = await SeedAccountAsync();
        var archived = PurchaseCondition.CreateForUser(Guid.NewGuid(), FakeCurrentUser.DefaultUserId, "Antiga");
        archived.Archive();
        await _conditions.AddAsync(archived);

        var createHandler = new CreateFixedIncomeHandler(_accountAccess, _fixedIncomes, _conditions, _unitOfWork);

        await Assert.ThrowsAsync<DomainException>(() => createHandler.ExecuteAsync(
            new CreateFixedIncomeCommand(account.Id, 2026, 10, null, null, 5, 1000m, "Teste", archived.Id)));

        await Assert.ThrowsAsync<DomainException>(() => createHandler.ExecuteAsync(
            new CreateFixedIncomeCommand(account.Id, 2026, 9, null, null, 5, 1000m, "Antes")));

        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task Handlers_UnknownFixedIncome_ThrowNotFoundException()
    {
        var unknownId = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateFixedIncomeHandler(_accountAccess, _fixedIncomes, _conditions, _clock, _unitOfWork)
                .ExecuteAsync(new UpdateFixedIncomeCommand(unknownId, "X", 5)));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new StopFixedIncomeHandler(_accountAccess, _fixedIncomes, _unitOfWork)
                .ExecuteAsync(new StopFixedIncomeCommand(unknownId, 2026, 12)));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteFixedIncomeHandler(_accountAccess, _fixedIncomes, _unitOfWork)
                .ExecuteAsync(new DeleteFixedIncomeCommand(unknownId)));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new ConfirmFixedIncomeReceiptHandler(_accountAccess, _fixedIncomes, _clock, _unitOfWork)
                .ExecuteAsync(new ConfirmFixedIncomeReceiptCommand(unknownId, 2026, 11, new DateOnly(2026, 11, 5), 100m)));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new RevertFixedIncomeReceiptHandler(_accountAccess, _fixedIncomes, _unitOfWork)
                .ExecuteAsync(new RevertFixedIncomeReceiptCommand(unknownId, 2026, 11)));
    }

    [Fact]
    public void DependencyInjection_RegistersAllFixedIncomeHandlers()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        Assert.Contains(services, d => d.ServiceType == typeof(CreateFixedIncomeHandler));
        Assert.Contains(services, d => d.ServiceType == typeof(UpdateFixedIncomeHandler));
        Assert.Contains(services, d => d.ServiceType == typeof(StopFixedIncomeHandler));
        Assert.Contains(services, d => d.ServiceType == typeof(DeleteFixedIncomeHandler));
        Assert.Contains(services, d => d.ServiceType == typeof(ConfirmFixedIncomeReceiptHandler));
        Assert.Contains(services, d => d.ServiceType == typeof(RevertFixedIncomeReceiptHandler));
        Assert.Contains(services, d => d.ServiceType == typeof(ListFixedIncomesHandler));
    }
}
