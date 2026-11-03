using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

/// <summary>
/// Tes ukuran kapsul per mode (<c>shared/SPEC.md</c> bagian 5 + mode ringkas Tahap 2).
/// </summary>
public class DisplayModeTests
{
    [Fact]
    public void StandardSizesFollowTheSpec()
    {
        var expanded = IslandMetrics.For(new Activity("a", ActivityKind.Media, state: ActivityState.Expanded));
        var compact = IslandMetrics.For(new Activity("a", ActivityKind.Media, state: ActivityState.Compact));

        Assert.Equal(new CapsuleSize(480, 152, 28, 0), expanded);
        Assert.Equal(new CapsuleSize(200, 34, 17, 0), compact);
        Assert.Equal(new CapsuleSize(34, 34, 17, 0), IslandMetrics.For(new Activity("a", ActivityKind.Media, state: ActivityState.Minimal)));
    }

    [Fact]
    public void RingkasIsSmallerThanStandardInEveryState()
    {
        foreach (var state in Enum.GetValues<ActivityState>())
        {
            var activity = new Activity("a", ActivityKind.Media, state: state);
            var standard = IslandMetrics.For(activity, DisplayMode.Standar);
            var ringkas = IslandMetrics.For(activity, DisplayMode.Ringkas);

            Assert.True(ringkas.Width < standard.Width, $"lebar ringkas harus lebih kecil di {state}");
            Assert.True(ringkas.Height <= standard.Height, $"tinggi ringkas tidak boleh lebih besar di {state}");
        }
    }

    [Fact]
    public void RingkasKeepsTheSameShapeForEachKind()
    {
        Assert.Equal(new CapsuleSize(220, 32, 16, 0), IslandMetrics.CompactFor(ActivityKind.Timer, DisplayMode.Ringkas));
        Assert.Equal(new CapsuleSize(252, 32, 16, 0), IslandMetrics.CompactFor(ActivityKind.Call, DisplayMode.Ringkas));
        Assert.Equal(new CapsuleSize(400, 128, 24, 0), IslandMetrics.ExpandedRingkas);
        Assert.Equal(new CapsuleSize(272, 72, 24, 0), IslandMetrics.ExpandedNotificationRingkas);
        Assert.Equal(8, IslandMetrics.FloatingTopInset);
    }

    [Fact]
    public void DisplayModeWireNamesRoundTrip()
    {
        Assert.True(DisplayModeExtensions.TryParseWireName("ringkas", out var ringkas));
        Assert.Equal(DisplayMode.Ringkas, ringkas);

        Assert.True(DisplayModeExtensions.TryParseWireName("STANDAR", out var standar));
        Assert.Equal(DisplayMode.Standar, standar);

        Assert.False(DisplayModeExtensions.TryParseWireName("hemat", out _));
        Assert.Equal("ringkas", DisplayMode.Ringkas.ToWireName());
    }
}
