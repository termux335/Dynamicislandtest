using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class IslandMetricsTests
{
    [Fact]
    public void ExpandedIsTheCompactCardSize()
    {
        var size = IslandMetrics.For(new Activity("a", ActivityKind.Media, state: ActivityState.Expanded));

        Assert.Equal(new CapsuleSize(480, 152, 28, 0), size);
        Assert.Equal(new CapsuleSize(400, 128, 24, 0), IslandMetrics.ExpandedRingkas);
    }

    [Fact]
    public void CompactSizesFollowKind()
    {
        Assert.Equal(new CapsuleSize(200, 34, 17, 0), IslandMetrics.CompactFor(ActivityKind.Media));
        Assert.Equal(new CapsuleSize(260, 36, 18, 0), IslandMetrics.CompactFor(ActivityKind.Timer));
        Assert.Equal(new CapsuleSize(300, 36, 18, 0), IslandMetrics.CompactFor(ActivityKind.Call));
    }

    [Fact]
    public void MinimalIsACircle()
    {
        var size = IslandMetrics.For(new Activity("a", ActivityKind.Media, state: ActivityState.Minimal));

        Assert.Equal(34, size.Width);
        Assert.Equal(size.Width, size.Height);
    }

    [Fact]
    public void ExpandedNotificationIsSmallerCard()
    {
        var size = IslandMetrics.For(new Activity("a", ActivityKind.Notification, state: ActivityState.Expanded));

        Assert.Equal(new CapsuleSize(312, 84, 28, 0), size);
    }

    [Fact]
    public void CapsuleFloatsEightPixelsFromTop()
    {
        Assert.Equal(8, IslandMetrics.FloatingTopInset);
    }

    [Fact]
    public void PaletteMatchesSpec()
    {
        Assert.Equal(0x000000u, IslandPalette.Background);
        Assert.Equal(0xFFFFFFu, IslandPalette.TextPrimary);
        Assert.Equal(0xB0B0B0u, IslandPalette.TextSecondary);
        Assert.Equal(0xFF9F43u, IslandPalette.Accent);
    }
}
