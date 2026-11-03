using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class PlacementTests
{
    [Fact]
    public void DefaultsToTopCenterWithoutForegroundInfo()
    {
        Assert.Equal(PlacementAnchor.TopCenter, PlacementRules.Choose(null));
    }

    [Fact]
    public void AutoPlacementOffAlwaysKeepsTopCenter()
    {
        var game = new ForegroundInfo(ForegroundCategory.Game, IsFullscreen: true, IsMaximized: false);
        var browser = new ForegroundInfo(ForegroundCategory.Browser, IsFullscreen: false, IsMaximized: true);

        Assert.Equal(PlacementAnchor.TopCenter, PlacementRules.Choose(game, autoPlacement: false));
        Assert.Equal(PlacementAnchor.TopCenter, PlacementRules.Choose(browser, autoPlacement: false));
    }

    [Fact]
    public void FullscreenGameDocksLeft()
    {
        var game = new ForegroundInfo(ForegroundCategory.Game, IsFullscreen: true, IsMaximized: false);

        Assert.Equal(PlacementAnchor.MidLeft, PlacementRules.Choose(game));
    }

    [Fact]
    public void MaximizedBrowserDocksLeft()
    {
        var browser = new ForegroundInfo(ForegroundCategory.Browser, IsFullscreen: false, IsMaximized: true);

        Assert.Equal(PlacementAnchor.MidLeft, PlacementRules.Choose(browser));
    }

    [Fact]
    public void FullscreenBrowserStaysTopCenter()
    {
        var browser = new ForegroundInfo(ForegroundCategory.Browser, IsFullscreen: true, IsMaximized: false);

        Assert.Equal(PlacementAnchor.TopCenter, PlacementRules.Choose(browser));
    }

    [Fact]
    public void WindowedBrowserStaysTopCenter()
    {
        var browser = new ForegroundInfo(ForegroundCategory.Browser, IsFullscreen: false, IsMaximized: false);

        Assert.Equal(PlacementAnchor.TopCenter, PlacementRules.Choose(browser));
    }

    [Fact]
    public void OtherAppsKeepTopCenter()
    {
        var editor = new ForegroundInfo(ForegroundCategory.Other, IsFullscreen: false, IsMaximized: true);

        Assert.Equal(PlacementAnchor.TopCenter, PlacementRules.Choose(editor));
    }
}
