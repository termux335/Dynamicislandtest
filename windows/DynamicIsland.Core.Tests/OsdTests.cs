using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class OsdTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void VolumeActivityShowsPercentAndProgress()
    {
        var activity = Osd.ActivityFor(new OsdState(OsdKind.Volume, 40), Now);

        Assert.Equal("Volume", activity.Title);
        Assert.Equal("40%", activity.Subtitle);
        Assert.Equal(0.4, activity.Progress);
        Assert.Equal(Now.AddSeconds(2), activity.ExpiresAt);
        Assert.Equal(Osd.ActivityId, activity.Id);
    }

    [Fact]
    public void MutedStateOverridesThePercentLabel()
    {
        var activity = Osd.ActivityFor(new OsdState(OsdKind.Mute, 0, Muted: true), Now);

        Assert.Equal("Bisu", activity.Subtitle);
    }

    [Fact]
    public void OnlyRealChangesAreShown()
    {
        var state = new OsdState(OsdKind.Brightness, 70);

        Assert.False(Osd.Changed(state, state));
        Assert.True(Osd.Changed(state, state with { Percent = 71 }));
        Assert.True(Osd.Changed(null, state));
    }
}
