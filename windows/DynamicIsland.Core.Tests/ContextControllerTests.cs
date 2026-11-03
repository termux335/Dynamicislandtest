using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class ContextControllerTests
{
    private static IslandController ControllerWithMedia()
    {
        var controller = new IslandController();
        controller.Refresh(new Activity("media", ActivityKind.Media, title: "Lagu"));
        return controller;
    }

    [Fact]
    public void GameContextForcesMinimalAndSetsOpacity()
    {
        var controller = ControllerWithMedia();

        controller.SetContext(ContextResolver.Resolve(IslandContext.Game));

        Assert.Equal(ResponseLevel.ClickThrough, controller.Presentation.ResponseLevel);
        Assert.Equal(ActivityState.Minimal, controller.Presentation.Primary?.State);
        Assert.True(controller.Presentation.ClickThrough);
        Assert.Equal(0.4, controller.Presentation.Opacity);
    }

    [Fact]
    public void CriticalOnlyHideNotificationsButKeepsCalls()
    {
        var controller = ControllerWithMedia();
        controller.Refresh(new Activity("notif", ActivityKind.Notification, priority: 90, title: "Pesan"));

        controller.SetContext(ContextResolver.Resolve(IslandContext.Game, settings: new ContextAwarenessSettings(
            Game: ResponseLevel.CriticalOnly)));

        Assert.Null(controller.Presentation.Primary);

        controller.Refresh(new Activity("call", ActivityKind.Call, title: "Rapat"));

        Assert.Equal("call", controller.Presentation.Primary?.Id);
    }

    [Fact]
    public void LowBatteryCountsAsCritical()
    {
        var controller = new IslandController();
        controller.Refresh(BatteryAlert.Low(8, DateTimeOffset.Now));

        controller.SetContext(ContextResolver.Resolve(IslandContext.Presentation, settings: new ContextAwarenessSettings(
            Presentation: ResponseLevel.CriticalOnly)));

        Assert.Equal(BatteryAlert.Id, controller.Presentation.Primary?.Id);
    }

    [Fact]
    public void HiddenContextLeavesNothingOnScreen()
    {
        var controller = ControllerWithMedia();

        controller.SetContext(ContextResolver.Resolve(IslandContext.Game, settings: new ContextAwarenessSettings(
            Game: ResponseLevel.Hidden)));

        Assert.Null(controller.Presentation.Primary);
        Assert.False(controller.Presentation.Visible);
    }

    [Fact]
    public void ClearingTheContextRestoresTheOriginalState()
    {
        var controller = ControllerWithMedia();
        controller.TapPrimary();
        Assert.Equal(ActivityState.Expanded, controller.Presentation.Primary?.State);

        controller.SetContext(ContextResolver.Resolve(IslandContext.Game));
        Assert.Equal(ActivityState.Minimal, controller.Presentation.Primary?.State);

        controller.ClearContext();

        Assert.Equal(IslandContext.Normal, controller.Presentation.Context);
        Assert.Equal(ActivityState.Expanded, controller.Presentation.Primary?.State);
        Assert.True(controller.Presentation.Visible);
        Assert.False(controller.Presentation.ClickThrough);
    }

    [Fact]
    public void SettingTheSameContextTwiceDoesNotPublishAgain()
    {
        var controller = ControllerWithMedia();
        var published = 0;
        controller.Changed += _ => published++;

        var resolution = ContextResolver.Resolve(IslandContext.Game);
        Assert.True(controller.SetContext(resolution));
        Assert.False(controller.SetContext(resolution));

        Assert.Equal(1, published);
    }

    [Fact]
    public void TapIsStillRecordedWhileTheContextForcesCompact()
    {
        var controller = ControllerWithMedia();
        controller.SetContext(ContextResolver.Resolve(IslandContext.Game));

        controller.TapPrimary();

        // Presentasi tetap minimal, tetapi state antrean ikut terbuka.
        Assert.Equal(ActivityState.Minimal, controller.Presentation.Primary?.State);
        Assert.Equal(ActivityState.Expanded, controller.Queue.Primary?.State);
    }
}
