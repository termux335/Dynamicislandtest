using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class ContextAwarenessTests
{
    private static ForegroundInfo GameFullscreen => new(ForegroundCategory.Game, true, false);

    private static ForegroundInfo BrowserFullscreen => new(ForegroundCategory.Browser, true, false);

    [Fact]
    public void DimmedLevelIsCompactishAndTransparent()
    {
        var effect = ResponseEffect.For(ResponseLevel.Dimmed);

        Assert.True(effect.Visible);
        Assert.False(effect.ClickThrough);
        Assert.Equal(0.4, effect.Opacity);
        Assert.Equal(ActivityState.Minimal, effect.ForcedState);
        Assert.False(effect.CriticalOnly);
    }

    [Fact]
    public void ClickThroughNeverBlocksTheMouse()
    {
        var effect = ResponseEffect.For(ResponseLevel.ClickThrough);

        Assert.True(effect.ClickThrough);
        Assert.Equal(0.4, effect.Opacity);
        Assert.Equal(ActivityState.Minimal, effect.ForcedState);
    }

    [Fact]
    public void HiddenLevelIsInvisibleAndCriticalOnly()
    {
        var effect = ResponseEffect.For(ResponseLevel.Hidden);

        Assert.False(effect.Visible);
        Assert.Equal(0, effect.Opacity);
        Assert.True(effect.CriticalOnly);
    }

    [Fact]
    public void ScreenShareOutranksEveryOtherSignal()
    {
        var signals = new ContextSignals(
            IsScreenSharing: true,
            InMeeting: true,
            Foreground: GameFullscreen,
            Notifications: UserNotificationState.PresentationMode);

        Assert.Equal(IslandContext.ScreenShare, ContextRules.Classify(signals));
    }

    [Fact]
    public void PresentationModeIsDetected()
    {
        var signals = new ContextSignals(Notifications: UserNotificationState.PresentationMode);

        Assert.Equal(IslandContext.Presentation, ContextRules.Classify(signals));
    }

    [Fact]
    public void MicrophoneActivityMeansMeeting()
    {
        Assert.Equal(IslandContext.Meeting, ContextRules.Classify(new ContextSignals(InMeeting: true)));
    }

    [Fact]
    public void FullscreenGameIsDetectedFromTheWindow()
    {
        Assert.Equal(
            IslandContext.Game,
            ContextRules.Classify(new ContextSignals(Foreground: GameFullscreen)));
    }

    [Fact]
    public void BrowserFullscreenIsVideo()
    {
        Assert.Equal(
            IslandContext.VideoFullscreen,
            ContextRules.Classify(new ContextSignals(Foreground: BrowserFullscreen)));
    }

    [Fact]
    public void WindowsFullscreenStateIsAFallback()
    {
        Assert.Equal(
            IslandContext.Game,
            ContextRules.Classify(new ContextSignals(Notifications: UserNotificationState.RunningD3DFullScreen)));

        Assert.Equal(
            IslandContext.VideoFullscreen,
            ContextRules.Classify(new ContextSignals(Notifications: UserNotificationState.Busy)));
    }

    [Fact]
    public void PerAppRuleOverridesLevelAndOpacity()
    {
        var settings = new ContextAwarenessSettings(
            Game: ResponseLevel.ClickThrough,
            Apps: new[] { new AppContextRule("Valorant", Level: ResponseLevel.Normal, Opacity: 0.9) });

        var resolution = ContextResolver.Resolve(IslandContext.Game, "Valorant", settings);

        Assert.Equal(ResponseLevel.Normal, resolution.Level);
        Assert.Equal(0.9, resolution.Effect.Opacity);
        Assert.False(resolution.Effect.ClickThrough);
    }

    [Fact]
    public void UnknownAppFallsBackToTheContextLevel()
    {
        var settings = new ContextAwarenessSettings(Game: ResponseLevel.Hidden);

        var resolution = ContextResolver.Resolve(IslandContext.Game, "lainnya", settings);

        Assert.Equal(ResponseLevel.Hidden, resolution.Level);
        Assert.False(resolution.Effect.Visible);
    }

    [Fact]
    public void DisabledContextAwarenessIsAlwaysNormal()
    {
        var settings = new ContextAwarenessSettings(Enabled: false, Game: ResponseLevel.Hidden);

        var resolution = ContextResolver.Resolve(IslandContext.Game, "Game", settings);

        Assert.Equal(IslandContext.Normal, resolution.Context);
        Assert.Equal(ResponseLevel.Normal, resolution.Level);
        Assert.True(resolution.Effect.Visible);
    }

    [Fact]
    public void ShareModeHidesNotificationContents()
    {
        Assert.True(ContextRules.HidesNotificationContent(IslandContext.ScreenShare));
        Assert.True(ContextRules.HidesNotificationContent(IslandContext.Presentation));
        Assert.False(ContextRules.HidesNotificationContent(IslandContext.Game));
        Assert.False(ContextRules.HidesNotificationContent(IslandContext.Normal));
    }

    [Fact]
    public void GameIsHeldForThreeSecondsAfterLeaving()
    {
        var now = new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);
        var detector = new ContextDetector(() => now);

        Assert.Equal(IslandContext.Game, detector.Observe(IslandContext.Game));

        now = now.AddSeconds(1);
        Assert.Equal(IslandContext.Game, detector.Observe(IslandContext.Normal));

        now = now.AddSeconds(2);
        Assert.Equal(IslandContext.Game, detector.Observe(IslandContext.Normal));

        now = now.AddSeconds(1);
        Assert.Equal(IslandContext.Normal, detector.Observe(IslandContext.Normal));
    }

    [Fact]
    public void EnteringGameIsImmediate()
    {
        var now = new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);
        var detector = new ContextDetector(() => now);

        Assert.Equal(IslandContext.Game, detector.Observe(IslandContext.Game));
        Assert.Equal(IslandContext.Game, detector.Current);
    }

    [Fact]
    public void DetectorRaisesChangedOnlyWhenTheContextActuallyChanges()
    {
        var now = new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);
        var detector = new ContextDetector(() => now);
        var changes = 0;
        detector.Changed += _ => changes++;

        detector.Observe(IslandContext.Meeting);
        now = now.AddSeconds(1);
        detector.Observe(IslandContext.Meeting);
        now = now.AddSeconds(1);
        detector.Observe(IslandContext.Normal);

        Assert.Equal(2, changes);
    }
}
