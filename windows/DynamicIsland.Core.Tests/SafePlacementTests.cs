using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class SafePlacementTests
{
    private static readonly ScreenArea Screen = new(0, 0, 1920, 1080);

    [Fact]
    public void PreferredSpotIsUsedWhenNothingIsForbidden()
    {
        Assert.Equal(SafePosition.TopCenter, SafePlacement.Choose(Screen, 200, 34));
    }

    [Fact]
    public void TopLeftIsTheFirstFallbackWhenTheTopCenterIsForbidden()
    {
        var zones = new[] { new ForbiddenZone("game", 700, 0, 520, 200) };

        Assert.Equal(SafePosition.TopLeft, SafePlacement.Choose(Screen, 200, 34, forbidden: zones, process: "game"));
    }

    [Fact]
    public void ForbiddenZonesOnlyApplyToTheirProcess()
    {
        var zones = new[] { new ForbiddenZone("game", 700, 0, 520, 200) };

        Assert.Equal(SafePosition.TopCenter, SafePlacement.Choose(Screen, 200, 34, forbidden: zones, process: "lain"));
    }

    [Fact]
    public void BottomCenterIsTheLastSafeSpot()
    {
        var zones = new[]
        {
            new ForbiddenZone(string.Empty, 0, 0, 1920, 120),
            new ForbiddenZone(string.Empty, 0, 0, 1920, 1080),
        };

        // Semua zona terlarang: pilihan pengguna tetap dipakai supaya tidak hilang.
        Assert.Equal(SafePosition.TopCenter, SafePlacement.Choose(Screen, 200, 34, forbidden: zones));
    }

    [Fact]
    public void BottomCenterCoordinatesSitAboveTheBottomEdge()
    {
        var (left, top) = SafePlacement.Spot(Screen, 200, 34, SafePosition.BottomCenter);

        Assert.Equal(860, left);
        Assert.Equal(1080 - 34 - SafePlacement.EdgeInset, top);
    }

    [Fact]
    public void CapsuleDodgesACloseCursorHorizontally()
    {
        var capsule = new ScreenArea(860, 8, 200, 34);

        var (dx, dy) = SafePlacement.Dodge(capsule, 900, 20);

        Assert.True(dx > 0);
        Assert.Equal(0, dy);
    }

    [Fact]
    public void CapsuleStaysPutWhenTheCursorIsFarAway()
    {
        var capsule = new ScreenArea(860, 8, 200, 34);

        Assert.Equal((0d, 0d), SafePlacement.Dodge(capsule, 100, 600));
    }

    [Fact]
    public void ProfileStoreRoundTripsPerApp()
    {
        var folder = Path.Combine(Path.GetTempPath(), "island-profiles-" + Guid.NewGuid().ToString("N"));
        var file = Path.Combine(folder, "profiles.json");

        try
        {
            var store = new AppProfileStore(file);
            store.Remember(new AppProfile("Valorant", SafePosition.TopLeft, DisplayMode.Ringkas, 0.8, ResponseLevel.ClickThrough));

            Assert.True(store.Save());

            var loaded = new AppProfileStore(file);
            Assert.True(loaded.Load());

            var profile = loaded.For("valorant");
            Assert.NotNull(profile);
            Assert.Equal(SafePosition.TopLeft, profile!.Position);
            Assert.Equal(DisplayMode.Ringkas, profile.Mode);
            Assert.Equal(0.8, profile.Opacity);
            Assert.Equal(ResponseLevel.ClickThrough, profile.Level);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void RememberedProfilesAreNewestFirst()
    {
        var store = new AppProfileStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "p.json"));

        store.Remember(new AppProfile("a"));
        store.Remember(new AppProfile("b"));

        Assert.Equal(new[] { "b", "a" }, store.All.Select(profile => profile.Process));
    }
}
