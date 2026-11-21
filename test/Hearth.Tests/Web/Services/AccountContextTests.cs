using Bunit;
using Hearth.Web.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Hearth.Tests.Web.Services;

public sealed class AccountContextTests : WebTestBase
{
    [Fact]
    public async Task InitializeAsync_IsIdempotentAndSelectsFirstAccount()
    {
        var firstId = await SeedAccountAsync("Primeira", initializeContext: false);
        await SeedAccountAsync("Segunda", initializeContext: false);

        var notifications = 0;
        AccountContext.Changed += () => notifications++;

        Assert.True(await AccountContext.InitializeAsync());
        Assert.True(await AccountContext.InitializeAsync());

        Assert.True(AccountContext.IsInitialized);
        Assert.Equal(2, AccountContext.Accounts.Count);
        Assert.Equal(firstId, AccountContext.CurrentAccountId);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public async Task SelectAccount_ChangesCurrentAndNotifiesOnlyWhenDifferent()
    {
        var firstId = await SeedAccountAsync("Primeira", initializeContext: false);
        var secondId = await SeedAccountAsync("Segunda", initializeContext: false);
        await AccountContext.InitializeAsync();

        var notifications = 0;
        AccountContext.Changed += () => notifications++;

        AccountContext.SelectAccount(firstId);
        Assert.Equal(0, notifications);

        AccountContext.SelectAccount(secondId);
        Assert.Equal(secondId, AccountContext.CurrentAccountId);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public async Task AccountPageBase_UnsubscribesOnDispose()
    {
        var firstId = await SeedAccountAsync("Primeira");
        var secondId = await SeedAccountAsync("Segunda", initializeContext: false);
        await AccountContext.ReloadAsync(firstId);

        var cut = Render<ProbeAccountPage>();
        Assert.Equal(1, cut.Instance.LoadCount);

        AccountContext.SelectAccount(secondId);
        cut.WaitForAssertion(() => Assert.Equal(2, cut.Instance.LoadCount));

        cut.Instance.Dispose();

        AccountContext.SelectAccount(firstId);
        await Task.Delay(50);

        Assert.Equal(2, cut.Instance.LoadCount);
    }

    private sealed class ProbeAccountPage : AccountPageBase
    {
        public int LoadCount { get; private set; }

        protected override Task LoadDataAsync(CancellationToken cancellationToken)
        {
            LoadCount++;
            return Task.CompletedTask;
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, $"Loads:{LoadCount}");
        }
    }
}
