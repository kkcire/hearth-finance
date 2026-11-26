using Bunit;
using Hearth.Domain.ValueObjects;
using Hearth.Web.Components.Pages;
using MudBlazor;

namespace Hearth.Tests.Web.Pages;

public sealed class AccountPageTests : WebTestBase
{
    [Fact]
    public async Task Render_DisplaysCurrentAccountDetailsWithReadOnlyTrackingStartAndImmutableOpeningBalance()
    {
        await SeedAccountAsync(
            name: "Conta Pessoal",
            initialBalance: 850.25m,
            initialBalanceDate: new DateOnly(2026, 2, 10));

        var cut = Render<AccountPage>();

        cut.WaitForAssertion(() =>
        {
            var dateInput = cut.Find("input[data-testid='initial-balance-date-readonly']");
            Assert.Equal("01/02/2026 — Fevereiro de 2026", dateInput.GetAttribute("value"));
            Assert.True(dateInput.HasAttribute("readonly"));

            var openingInput = cut.Find("input[data-testid='initial-balance-readonly']");
            Assert.True(openingInput.HasAttribute("readonly"));
            Assert.Contains("850,25", openingInput.GetAttribute("value") ?? string.Empty);
        });
    }

    [Fact]
    public async Task DirectSetBalance_UpdatesSingleDay1OpeningBalanceWithoutDatePicker()
    {
        Clock.Today = new DateOnly(2026, 11, 20);
        var accountId = await SeedAccountAsync(
            name: "Conta Antiga",
            initialBalance: 100m,
            initialBalanceDate: new DateOnly(2026, 11, 10)); // TrackingStartDate = 2026-11-01

        var cut = Render<AccountPage>();

        // No mid-month date picker is offered — only Day 1 opening balance can be edited
        Assert.Empty(cut.FindAll("input[data-testid='account-set-balance-date-input']"));

        // Rename works
        cut.Find("input[data-testid='rename-account-input']").Input("Conta Nova");
        await cut.Find("[data-testid='rename-account-submit']").ClickAsync();

        // Update single Day-1 opening balance to R$ 1.842,50
        cut.Find("input[data-testid='account-set-balance-input']").Input("1842,50");
        cut.Find("input[data-testid='account-set-balance-reason-input']").Input("Conferência extrato dia 1");
        await cut.Find("[data-testid='account-set-balance-submit']").ClickAsync();

        var persisted = await Accounts.GetByIdAsync(accountId);
        Assert.NotNull(persisted);
        Assert.Equal("Conta Nova", persisted.Name);
        Assert.Equal(Money.FromDecimal(1842.50m), persisted.InitialBalance);
        var entry = Assert.Single(persisted.BalanceAdjustments);
        Assert.Equal(new DateOnly(2026, 11, 1), entry.Date);
        Assert.Equal(Money.FromDecimal(1842.50m), entry.Amount);
        Assert.Equal("Conferência extrato dia 1", entry.Reason);
        Assert.NotEmpty(cut.FindAll("[data-testid='account-balance-history-log']"));
        Assert.Contains("01/11/2026", cut.Find("[data-testid='account-balance-history-log']").TextContent);

        // Also supports setting Day-1 balance directly to R$ 0,00 and deleting an erroneous entry
        cut.Find("input[data-testid='account-set-balance-input']").Input("0");
        cut.Find("input[data-testid='account-set-balance-reason-input']").Input("Zerar conta");
        await cut.Find("[data-testid='account-set-balance-submit']").ClickAsync();
        Assert.Equal(Money.Zero, persisted.InitialBalance);
        Assert.Equal(2, persisted.BalanceAdjustments.Count);
        var zeroEntryId = persisted.BalanceAdjustments[^1].Id;
        Assert.Equal(Money.Zero, persisted.BalanceAdjustments[^1].Amount);

        // Deleting the second entry reverts InitialBalance back to R$ 1.842,50
        await cut.Find($"[data-testid='delete-balance-entry-{zeroEntryId}']").ClickAsync();
        await cut.Find($"[data-testid='confirm-delete-balance-entry-{zeroEntryId}']").ClickAsync();
        Assert.Equal(Money.FromDecimal(1842.50m), persisted.InitialBalance);
        Assert.Single(persisted.BalanceAdjustments);
    }
}
