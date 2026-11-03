using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class IslandEventTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UpsertDefaultsToTransientNotificationWithFourSecondTtl()
    {
        var parsed = IslandEvent.Parse("""{"op":"upsert","activity":{"id":"n1"}}""", Now);

        var activity = Assert.IsType<IslandEvent.Upsert>(parsed).Activity;
        Assert.Equal("n1", activity.Id);
        Assert.Equal(ActivityKind.Notification, activity.Kind);
        Assert.False(activity.IsLive);
        Assert.Equal(Now.AddMilliseconds(IslandEvent.DefaultTtlMs), activity.ExpiresAt);
        Assert.Equal(string.Empty, activity.Title);
        Assert.Equal(30, activity.Priority);
        Assert.Empty(activity.Actions);
    }

    [Fact]
    public void UpsertReadsEveryField()
    {
        const string line = """
        {"op":"upsert","activity":{
          "id":"track-1","kind":"media","priority":85,"title":"Lagu A","subtitle":"Artis B",
          "progress":0.42,"isLive":true,"ttlMs":1500,
          "actions":[{"id":"playpause","label":"Jeda","glyph":"pause"},{"id":"next"}]}}
        """;

        var activity = Assert.IsType<IslandEvent.Upsert>(IslandEvent.Parse(line, Now)).Activity;

        Assert.Equal(ActivityKind.Media, activity.Kind);
        Assert.Equal(85, activity.Priority);
        Assert.Equal("Lagu A", activity.Title);
        Assert.Equal("Artis B", activity.Subtitle);
        Assert.Equal(0.42, activity.Progress);
        Assert.True(activity.IsLive);
        Assert.Null(activity.ExpiresAt);
        Assert.Equal(ActivityState.Compact, activity.State);
        Assert.Equal(2, activity.Actions.Count);
        Assert.Equal(new ActivityAction("playpause", "Jeda", "pause"), activity.Actions[0]);
        Assert.Equal(new ActivityAction("next", "next", "circle"), activity.Actions[1]);
    }

    [Fact]
    public void TransientAlertUsesCustomTtl()
    {
        var activity = Assert.IsType<IslandEvent.Upsert>(
            IslandEvent.Parse("""{"op":"upsert","activity":{"id":"b1","kind":"battery","ttlMs":1500}}""", Now)).Activity;

        Assert.Equal(Now.AddMilliseconds(1500), activity.ExpiresAt);
    }

    [Fact]
    public void ProgressEventIsParsed()
    {
        var parsed = IslandEvent.Parse("""{"op":"progress","id":"track-1","progress":0.55}""", Now);

        Assert.Equal(new IslandEvent.Progress("track-1", 0.55), parsed);
    }

    [Fact]
    public void ClearExpireAndClearAllAreParsed()
    {
        Assert.Equal(new IslandEvent.Expire("a"), IslandEvent.Parse("""{"op":"expire","id":"a"}""", Now));
        Assert.Equal(new IslandEvent.Clear("a"), IslandEvent.Parse("""{"op":"clear","id":"a"}""", Now));
        Assert.Equal(new IslandEvent.ClearAll(), IslandEvent.Parse("""{"op":"clearAll"}""", Now));
    }

    [Fact]
    public void UnknownOpIsRejected()
    {
        var error = Assert.Throws<IslandEventException>(
            () => IslandEvent.Parse("""{"op":"meledak"}""", Now));

        Assert.Equal("op tidak dikenal: meledak", error.Message);
    }

    [Fact]
    public void UnknownKindIsRejected()
    {
        var error = Assert.Throws<IslandEventException>(
            () => IslandEvent.Parse("""{"op":"upsert","activity":{"id":"a","kind":"roket"}}""", Now));

        Assert.Equal("kind tidak dikenal: roket", error.Message);
    }

    [Fact]
    public void MissingRequiredFieldsAreReported()
    {
        Assert.Equal("field wajib tidak ada: op", Assert.Throws<IslandEventException>(
            () => IslandEvent.Parse("{}", Now)).Message);
        Assert.Equal("field wajib tidak ada: id", Assert.Throws<IslandEventException>(
            () => IslandEvent.Parse("""{"op":"clear"}""", Now)).Message);
        Assert.Equal("field wajib tidak ada: activity", Assert.Throws<IslandEventException>(
            () => IslandEvent.Parse("""{"op":"upsert"}""", Now)).Message);
        Assert.Equal("field wajib tidak ada: progress", Assert.Throws<IslandEventException>(
            () => IslandEvent.Parse("""{"op":"progress","id":"a"}""", Now)).Message);
    }

    [Fact]
    public void InvalidJsonIsReported()
    {
        var error = Assert.Throws<IslandEventException>(() => IslandEvent.Parse("bukan json", Now));
        Assert.StartsWith("JSON tidak valid:", error.Message);

        var arrayError = Assert.Throws<IslandEventException>(() => IslandEvent.Parse("[]", Now));
        Assert.Equal("JSON tidak valid: root harus objek", arrayError.Message);
    }

    [Fact]
    public void ProgressIsClampedToUnitRange()
    {
        var activity = Assert.IsType<IslandEvent.Upsert>(
            IslandEvent.Parse("""{"op":"upsert","activity":{"id":"a","kind":"download","progress":1.7}}""", Now)).Activity;

        Assert.Equal(1.0, activity.Progress);
    }
}
