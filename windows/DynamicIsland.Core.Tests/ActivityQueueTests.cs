using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class ActivityQueueTests
{
    [Fact]
    public void PrimaryIsHighestPriority()
    {
        var queue = new ActivityQueue();
        queue.Upsert(new Activity("media", ActivityKind.Media));
        queue.Upsert(new Activity("timer", ActivityKind.Timer));

        Assert.Equal("timer", queue.Primary?.Id);
        Assert.Equal("media", queue.Secondary?.Id);
    }

    [Fact]
    public void EqualPriorityIsFirstInFirstOut()
    {
        var queue = new ActivityQueue();
        queue.Upsert(new Activity("call-a", ActivityKind.Call));
        queue.Upsert(new Activity("call-b", ActivityKind.Call));

        Assert.Equal("call-a", queue.Primary?.Id);
        Assert.Equal(new[] { "call-a", "call-b" }, queue.Ordered.Select(activity => activity.Id));
    }

    [Fact]
    public void UpsertSameIdReplacesInPlaceAndKeepsFifo()
    {
        var queue = new ActivityQueue();
        queue.Upsert(new Activity("a", ActivityKind.Call, title: "lama"));
        queue.Upsert(new Activity("b", ActivityKind.Call));

        queue.Upsert(new Activity("a", ActivityKind.Call, title: "baru"));

        Assert.Equal(2, queue.Count);
        Assert.Equal(new[] { "a", "b" }, queue.Ordered.Select(activity => activity.Id));
        Assert.Equal("baru", queue.Find("a")?.Title);
    }

    [Fact]
    public void RemovingPrimaryPromotesNext()
    {
        var queue = new ActivityQueue();
        queue.Upsert(new Activity("low", ActivityKind.Battery));
        queue.Upsert(new Activity("high", ActivityKind.Call));

        queue.Remove("high");

        Assert.Equal("low", queue.Primary?.Id);
    }

    [Fact]
    public void OrderIsStableWhenNothingChanges()
    {
        var queue = new ActivityQueue();
        queue.Upsert(new Activity("a", ActivityKind.Notification));
        queue.Upsert(new Activity("b", ActivityKind.Notification));
        queue.Upsert(new Activity("c", ActivityKind.Notification));
        var before = queue.Ordered.Select(activity => activity.Id).ToArray();

        queue.UpdateProgress("b", 0.5);

        Assert.Equal(before, queue.Ordered.Select(activity => activity.Id));
    }

    [Fact]
    public void PurgeExpiredRemovesOnlyExpiredTransientAlerts()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var queue = new ActivityQueue();
        queue.Upsert(new Activity("live", ActivityKind.Media, expiresAt: now.AddSeconds(-10)));
        queue.Upsert(new Activity("old", ActivityKind.Battery, isLive: false, expiresAt: now.AddSeconds(-1)));
        queue.Upsert(new Activity("fresh", ActivityKind.Device, isLive: false, expiresAt: now.AddSeconds(5)));

        var purged = queue.PurgeExpired(now);

        Assert.Equal(new[] { "old" }, purged);
        Assert.Equal(new[] { "fresh", "live" }, queue.Ordered.Select(activity => activity.Id).OrderBy(id => id));
    }

    [Fact]
    public void NextExpiryIgnoresLiveActivities()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var queue = new ActivityQueue();
        queue.Upsert(new Activity("live", ActivityKind.Media, expiresAt: now.AddSeconds(-100)));
        queue.Upsert(new Activity("alert", ActivityKind.Notification, isLive: false, expiresAt: now.AddSeconds(4)));

        Assert.Equal(now.AddSeconds(4), queue.NextExpiry);
    }

    [Fact]
    public void ProgressIsClamped()
    {
        var queue = new ActivityQueue();
        queue.Upsert(new Activity("download", ActivityKind.Download));

        queue.UpdateProgress("download", 1.7);

        Assert.Equal(1.0, queue.Find("download")?.Progress);
    }

    [Fact]
    public void PromoteMovesSecondaryToPrimaryWithoutChangingPriority()
    {
        var queue = new ActivityQueue();
        queue.Upsert(new Activity("timer", ActivityKind.Timer));
        queue.Upsert(new Activity("media", ActivityKind.Media));

        Assert.True(queue.Promote("media"));

        Assert.Equal("media", queue.Primary?.Id);
        Assert.Equal("timer", queue.Secondary?.Id);
        Assert.Equal(new[] { "media", "timer" }, queue.Ordered.Select(activity => activity.Id));
        Assert.Equal(70, queue.Find("media")?.Priority);
    }

    [Fact]
    public void PromotedActivityStaysPrimaryWhenItsContentRefreshes()
    {
        var queue = new ActivityQueue();
        queue.Upsert(new Activity("timer", ActivityKind.Timer));
        queue.Upsert(new Activity("media", ActivityKind.Media));
        queue.Promote("media");

        queue.Upsert(new Activity("media", ActivityKind.Media, title: "Lagu B"));

        Assert.Equal("media", queue.Primary?.Id);
        Assert.Equal("Lagu B", queue.Primary?.Title);
    }

    [Fact]
    public void PromotionEndsWhenSomethingMoreImportantArrives()
    {
        var queue = new ActivityQueue();
        queue.Upsert(new Activity("timer", ActivityKind.Timer));
        queue.Upsert(new Activity("media", ActivityKind.Media));
        queue.Promote("media");

        // Notifikasi (30) tidak lebih penting dari batas saat promosi (80).
        queue.Upsert(new Activity("notif", ActivityKind.Notification));
        Assert.Equal("media", queue.Primary?.Id);

        // Panggilan (100) melewati batas, jadi promosi berakhir. Timer (80) yang
        // dipromosikan turun kembali ke urutan prioritas.
        queue.Upsert(new Activity("call", ActivityKind.Call));
        Assert.Equal("call", queue.Primary?.Id);
        Assert.Equal("timer", queue.Secondary?.Id);
        Assert.Null(queue.PromotedId);
    }

    [Fact]
    public void RemovingThePromotedActivityEndsPromotion()
    {
        var queue = new ActivityQueue();
        queue.Upsert(new Activity("timer", ActivityKind.Timer));
        queue.Upsert(new Activity("media", ActivityKind.Media));
        queue.Promote("media");

        queue.Remove("media");

        Assert.Null(queue.PromotedId);
        Assert.Equal("timer", queue.Primary?.Id);
    }

    [Fact]
    public void PromotingAnUnknownIdIsIgnored()
    {
        var queue = new ActivityQueue();
        queue.Upsert(new Activity("timer", ActivityKind.Timer));

        Assert.False(queue.Promote("tidak-ada"));
        Assert.Equal("timer", queue.Primary?.Id);
    }

    [Fact]
    public void ClearPromotionRestoresPriorityOrder()
    {
        var queue = new ActivityQueue();
        queue.Upsert(new Activity("timer", ActivityKind.Timer));
        queue.Upsert(new Activity("media", ActivityKind.Media));
        queue.Promote("media");

        queue.ClearPromotion();

        Assert.Equal("timer", queue.Primary?.Id);
        Assert.Equal("media", queue.Secondary?.Id);
    }

    [Fact]
    public void DefaultPriorityFollowsKind()
    {
        Assert.Equal(100, new Activity("x", ActivityKind.Call).Priority);
        Assert.Equal(10, new Activity("x", ActivityKind.Battery).Priority);
        Assert.Equal(5, new Activity("x", ActivityKind.Media, priority: 5).Priority);
    }
}
