using Bunit;
using Hearth.Domain.ValueObjects;
using Hearth.Web.Components.Pages;

namespace Hearth.Tests.Web.Pages;

public sealed class AccountPageTests : WebTestBase
{
    [Fact]
    public async Task Render_DisplaysCurrentAccountDetailsWithReadOnlyInitialDate()
    {
        await SeedAccountAsync(
            name: "Conta Pessoal",
            initialBalance: 850.25m,
            initialBalanceDate: new DateOnly(2026, 2, 10),
            closingDay: 3,
            dueDay: 12);

        var cut = Render<AccountPage>();

        cut.WaitForAssertion(() =>
        {
            var dateInput = cut.Find("input[data-testid='initial-balance-date-readonly']");
            Assert.Equal("10/02/2026", dateInput.GetAttribute("value"));
            Assert.True(dateInput.HasAttribute("readonly"));
        });
    }

    [Fact]
    public async Task UpdateActions_PersistRenameBalanceAndBillingRule()
    {
        var accountId = await SeedAccountAsync(
            name: "Conta Antiga",
            initialBalance: 100m,
            initialBalanceDate: new DateOnly(2026, 1, 1),
            closingDay: 3,
            dueDay: 12);

        var cut = Render<AccountPage>();

        cut.Find("input[data-testid='rename-account-input']").Input("Conta Nova");
        await cut.Find("[data-testid='rename-account-submit']").ClickAsync();

        cut.Find("input[data-testid='update-balance-input']").Change("2.500,75");
        await cut.Find("[data-testid='update-balance-submit']").ClickAsync();

        cut.Find("input[data-testid='update-closing-day-input']").Change("8");
        cut.Find("input[data-testid='update-due-day-input']").Change("18");
        await cut.Find("[data-testid='update-billing-rule-submit']").ClickAsync();

        var persisted = await Accounts.GetByIdAsync(accountId);
        Assert.NotNull(persisted);
        Assert.Equal("Conta Nova", persisted.Name);
        Assert.Equal(Money.FromDecimal(2500.75m), persisted.InitialBalance);
        Assert.Equal(8, persisted.BillingRule.ClosingDay);
        Assert.Equal(18, persisted.BillingRule.DueDay);
        Assert.Equal("Conta Nova", AccountContext.Current?.Name);
    }
}
