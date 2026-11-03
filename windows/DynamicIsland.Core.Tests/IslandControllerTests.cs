using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class IslandControllerTests
{
    private sealed class TestClock
    {
        public TestClock(DateTimeOffset start) => Now = start;

        public DateTimeOffset Now { get; private set; }

        public DateTimeOffset Read() => Now;

        public void Advance(TimeSpan delta) => Now += delta;
    }

    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static (IslandController Controller, TestClock Clock) Create()
    {
        var clock = new TestClock(Start);
        return (new IslandController(clock.Read), clock);
    }

    private static Activity Media(string id = "track-1") => new(id, ActivityKind.Media, title: "Lagu A");

    [Fact]
    public void UpsertedActivityStartsCompact()
    {
        var (controller, _) = Create();

        controller.Handle(new IslandEvent.Upsert(Media()));

        Assert.Equal(ActivityState.Compact, controller.Presentation.Primary?.State);
        Assert.NotNull(controller.Queue.Find("track-1"));
    }

    [Fact]
    public void TapOpensOneStep()
    {
        var (controller, _) = Create();
        controller.Handle(new IslandEvent.Upsert(Media()));

        controller.TapPrimary();

        Assert.Equal(ActivityState.Expanded, controller.Presentation.Primary?.State);
        Assert.Equal(Start.AddSeconds(4), controller.AutoCollapseAt);
    }

    [Fact]
    public void ExpandedCollapsesAfterFourSecondsWithoutInteraction()
    {
        var (controller, clock) = Create();
        controller.Handle(new IslandEvent.Upsert(Media()));
        controller.TapPrimary();

        clock.Advance(TimeSpan.FromMilliseconds(3900));
        controller.Tick();
        Assert.Equal(ActivityState.Expanded, controller.Presentation.Primary?.State);

        clock.Advance(TimeSpan.FromMilliseconds(100));
        controller.Tick();
        Assert.Equal(ActivityState.Compact, controller.Presentation.Primary?.State);
        Assert.Null(controller.AutoCollapseAt);
    }

    [Fact]
    public void InteractionRestartsTheAutoCollapseClock()
    {
        var (controller, clock) = Create();
        controller.Handle(new IslandEvent.Upsert(Media()));

        controller.TapPrimary();                 // expanded pada t=0
        clock.Advance(TimeSpan.FromSeconds(3));
        controller.Tick();

        controller.ClosePrimary();               // compact pada t=3
        clock.Advance(TimeSpan.FromMilliseconds(100));
        controller.TapPrimary();                 // expanded lagi pada t=3.1

        clock.Advance(TimeSpan.FromSeconds(3.5));
        controller.Tick();

        Assert.Equal(ActivityState.Expanded, controller.Presentation.Primary?.State);
    }

    [Fact]
    public void ExpiredTransientAlertIsPurgedByTick()
    {
        var (controller, clock) = Create();
        controller.HandleLine("""{"op":"upsert","activity":{"id":"n1","title":"Pesan baru","ttlMs":4000}}""");
        Assert.Equal("n1", controller.Presentation.Primary?.Id);

        clock.Advance(TimeSpan.FromMilliseconds(4000));
        controller.Tick();

        Assert.Null(controller.Presentation.Primary);
        Assert.True(controller.Queue.IsEmpty);
    }

    [Fact]
    public void ClearingPrimaryPromotesNextActivity()
    {
        var (controller, _) = Create();
        controller.HandleLine("""{"op":"upsert","activity":{"id":"track-1","kind":"media"}}""");
        controller.HandleLine("""{"op":"upsert","activity":{"id":"timer-1","kind":"timer"}}""");

        controller.HandleLine("""{"op":"clear","id":"timer-1"}""");

        Assert.Equal("track-1", controller.Presentation.Primary?.Id);
    }

    [Fact]
    public void InvalidLineIsReportedAndIgnored()
    {
        var (controller, _) = Create();
        var errors = new List<string>();
        controller.OnError = errors.Add;

        controller.HandleLine("""{"op":"upsert","activity":{"id":"track-1","kind":"media"}}""");
        controller.HandleLine("""{"op":"tidak-ada"}""");

        Assert.Single(errors);
        Assert.Equal("op tidak dikenal: tidak-ada", errors[0]);
        Assert.Equal("track-1", controller.Presentation.Primary?.Id);
    }

    [Fact]
    public void ProgressEventUpdatesPrimaryWithoutChangingState()
    {
        var (controller, _) = Create();
        controller.HandleLine("""{"op":"upsert","activity":{"id":"track-1","kind":"media","progress":0.2}}""");

        controller.HandleLine("""{"op":"progress","id":"track-1","progress":0.8}""");

        Assert.Equal(0.8, controller.Presentation.Primary?.Progress);
        Assert.Equal(ActivityState.Compact, controller.Presentation.Primary?.State);
    }

    [Fact]
    public void ChangedFiresOnlyWhenPresentationChanges()
    {
        var (controller, _) = Create();
        var changes = 0;
        controller.Changed += _ => changes++;

        controller.Handle(new IslandEvent.Upsert(Media()));
        controller.Tick();
        controller.Tick();

        Assert.Equal(1, changes);
    }

    [Fact]
    public void SecondaryIsHiddenUntilTwoCapsulesAreEnabled()
    {
        var (controller, _) = Create();
        controller.HandleLine("""{"op":"upsert","activity":{"id":"track-1","kind":"media"}}""");
        controller.HandleLine("""{"op":"upsert","activity":{"id":"timer-1","kind":"timer"}}""");

        Assert.Null(controller.Presentation.Secondary);

        controller.ShowsSecondary = true;
        controller.HandleLine("""{"op":"progress","id":"track-1","progress":0.9}""");

        Assert.Equal("track-1", controller.Presentation.Secondary?.Id);
    }

    [Fact]
    public void EnablingTwoCapsulesPublishesTheSecondaryRightAway()
    {
        var (controller, _) = Create();
        controller.Handle(new IslandEvent.Upsert(Media()));
        controller.Handle(new IslandEvent.Upsert(new Activity("timer-1", ActivityKind.Timer, title: "Fokus")));

        var changes = 0;
        controller.Changed += _ => changes++;
        controller.ShowsSecondary = true;

        Assert.Equal(1, changes);
        Assert.Equal("track-1", controller.Presentation.Secondary?.Id);
        Assert.Equal(ActivityState.Compact, controller.Presentation.Secondary?.State);
    }

    [Fact]
    public void SecondaryIsTheSecondHighestPriorityNotTheNextInserted()
    {
        var (controller, _) = Create();
        controller.Handle(new IslandEvent.Upsert(new Activity("call-1", ActivityKind.Call, title: "Rapat")));
        controller.Handle(new IslandEvent.Upsert(new Activity("media-1", ActivityKind.Media, title: "Lagu")));
        controller.ShowsSecondary = true;

        Assert.Equal("call-1", controller.Presentation.Primary?.Id);
        Assert.Equal("media-1", controller.Presentation.Secondary?.Id);
    }

    [Fact]
    public void DisablingTwoCapsulesHidesTheSecondary()
    {
        var (controller, _) = Create();
        controller.Handle(new IslandEvent.Upsert(Media()));
        controller.Handle(new IslandEvent.Upsert(new Activity("timer-1", ActivityKind.Timer, title: "Fokus")));
        controller.ShowsSecondary = true;
        Assert.NotNull(controller.Presentation.Secondary);

        controller.ShowsSecondary = false;

        Assert.Null(controller.Presentation.Secondary);
    }

    [Fact]
    public void PromotingSecondarySwapsTheCapsules()
    {
        var (controller, _) = Create();
        controller.Handle(new IslandEvent.Upsert(Media()));
        controller.Handle(new IslandEvent.Upsert(new Activity("timer-1", ActivityKind.Timer, title: "Fokus")));
        controller.ShowsSecondary = true;

        Assert.True(controller.PromoteSecondary());

        Assert.Equal("track-1", controller.Presentation.Primary?.Id);
        Assert.Equal("timer-1", controller.Presentation.Secondary?.Id);
        Assert.Equal(ActivityState.Compact, controller.Presentation.Primary?.State);
    }

    [Fact]
    public void PromotionOpensTheTappedActivityAsCompact()
    {
        var (controller, _) = Create();
        controller.Handle(new IslandEvent.Upsert(Media()));
        controller.Handle(new IslandEvent.Upsert(new Activity("timer-1", ActivityKind.Timer, title: "Fokus")));
        controller.ShowsSecondary = true;
        controller.Queue.SetState("track-1", ActivityState.Minimal);

        controller.PromoteSecondary();

        Assert.Equal(ActivityState.Compact, controller.Presentation.Primary?.State);
    }

    [Fact]
    public void PromotionNeedsTheTwoCapsuleFeature()
    {
        var (controller, _) = Create();
        controller.Handle(new IslandEvent.Upsert(Media()));
        controller.Handle(new IslandEvent.Upsert(new Activity("timer-1", ActivityKind.Timer, title: "Fokus")));

        Assert.False(controller.PromoteSecondary());
        Assert.Equal("timer-1", controller.Presentation.Primary?.Id);
    }

    [Fact]
    public void PromotionWithoutASecondActivityIsIgnored()
    {
        var (controller, _) = Create();
        controller.Handle(new IslandEvent.Upsert(Media()));
        controller.ShowsSecondary = true;

        Assert.False(controller.PromoteSecondary());
        Assert.Equal("track-1", controller.Presentation.Primary?.Id);
    }

    [Fact]
    public void ClearingPromotionRestoresThePriorityOrder()
    {
        var (controller, _) = Create();
        controller.Handle(new IslandEvent.Upsert(Media()));
        controller.Handle(new IslandEvent.Upsert(new Activity("timer-1", ActivityKind.Timer, title: "Fokus")));
        controller.ShowsSecondary = true;
        controller.PromoteSecondary();

        controller.ClearPromotion();

        Assert.Equal("timer-1", controller.Presentation.Primary?.Id);
        Assert.Equal("track-1", controller.Presentation.Secondary?.Id);
    }

    [Fact]
    public void ClearAllEmptiesTheQueue()
    {
        var (controller, _) = Create();
        controller.Handle(new IslandEvent.Upsert(Media()));

        controller.Handle(new IslandEvent.ClearAll());

        Assert.Null(controller.Presentation.Primary);
        Assert.True(controller.Queue.IsEmpty);
    }

    [Fact]
    public void AutoCollapseNowOnlyAffectsExpanded()
    {
        var (controller, _) = Create();
        controller.Handle(new IslandEvent.Upsert(Media()));

        controller.AutoCollapseNow();
        Assert.Equal(ActivityState.Compact, controller.Presentation.Primary?.State);

        controller.TapPrimary();
        controller.AutoCollapseNow();
        Assert.Equal(ActivityState.Compact, controller.Presentation.Primary?.State);
    }

    [Fact]
    public void KeepExpandedRestartsTheAutoCollapseClock()
    {
        var (controller, clock) = Create();
        controller.Handle(new IslandEvent.Upsert(Media()));
        controller.TapPrimary();

        clock.Advance(TimeSpan.FromSeconds(3.5));
        controller.KeepExpanded();
        Assert.Equal(Start.AddSeconds(3.5).AddSeconds(4), controller.AutoCollapseAt);

        clock.Advance(TimeSpan.FromMilliseconds(600));
        controller.Tick();

        Assert.Equal(ActivityState.Expanded, controller.Presentation.Primary?.State);
    }

    [Fact]
    public void RefreshKeepsTheDisplayedStateWhileUpdatingContent()
    {
        var (controller, _) = Create();
        controller.Handle(new IslandEvent.Upsert(Media()));
        controller.TapPrimary();
        Assert.Equal(ActivityState.Expanded, controller.Presentation.Primary?.State);

        controller.Refresh(Media() with { Subtitle = "Artis B · 00:42" });

        Assert.Equal(ActivityState.Expanded, controller.Presentation.Primary?.State);
        Assert.Equal("Artis B · 00:42", controller.Presentation.Primary?.Subtitle);
        Assert.Equal(1, controller.Queue.Count);
    }

    [Fact]
    public void RefreshInsertsAnUnknownIdAsCompact()
    {
        var (controller, _) = Create();

        controller.Refresh(BatteryAlert.Low(level: 12, now: Start));

        Assert.Equal("battery", controller.Presentation.Primary?.Id);
        Assert.Equal(ActivityState.Compact, controller.Presentation.Primary?.State);
        Assert.Equal(Start.AddMilliseconds(IslandEvent.DefaultTtlMs), controller.Presentation.Primary?.ExpiresAt);
    }

    [Fact]
    public void RemovingAnIdPromotesTheNextActivity()
    {
        var (controller, _) = Create();
        controller.Handle(new IslandEvent.Upsert(Media()));
        controller.Handle(new IslandEvent.Upsert(new Activity("timer-1", ActivityKind.Timer, title: "Fokus")));

        controller.Remove("timer-1");

        Assert.Equal("track-1", controller.Presentation.Primary?.Id);
    }

    [Fact]
    public void DisplayModeChangeIsPublishedOnce()
    {
        var (controller, _) = Create();
        var changes = 0;
        controller.Changed += _ => changes++;
        Assert.Equal(DisplayMode.Standar, controller.Presentation.Mode);

        controller.DisplayMode = DisplayMode.Ringkas;
        controller.DisplayMode = DisplayMode.Ringkas;

        Assert.Equal(1, changes);
        Assert.Equal(DisplayMode.Ringkas, controller.Presentation.Mode);
    }
}
