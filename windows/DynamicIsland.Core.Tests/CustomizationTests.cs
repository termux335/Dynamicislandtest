using System.Globalization;
using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class CustomizationTests
{
    [Fact]
    public void DefaultAccentMatchesTheSpec()
    {
        Assert.Equal(IslandPalette.Accent, AccentChoice.Oranye.Rgb());
    }

    [Fact]
    public void AccentChoicesAreDistinctAndVisibleOnBlack()
    {
        var colors = Enum.GetValues<AccentChoice>().Select(choice => choice.Rgb()).ToList();

        Assert.Equal(colors.Count, colors.Distinct().Count());
        Assert.DoesNotContain(0u, colors);
        Assert.Equal(colors.Count, colors.Select(color => color.ToString("X6", CultureInfo.InvariantCulture)).Distinct().Count());
    }

    [Fact]
    public void PositionChoiceMapsToAnAnchor()
    {
        Assert.Equal(PlacementAnchor.TopCenter, PositionChoice.TopCenter.ToAnchor());
        Assert.Equal(PlacementAnchor.TopLeft, PositionChoice.TopLeft.ToAnchor());
        Assert.Equal(PlacementAnchor.TopRight, PositionChoice.TopRight.ToAnchor());
    }

    [Fact]
    public void PreferredPositionIsUsedWhenThereIsNoSpecialContext()
    {
        Assert.Equal(PlacementAnchor.TopRight, PlacementRules.Choose(null, preferred: PositionChoice.TopRight));
        Assert.Equal(PlacementAnchor.TopLeft, PlacementRules.Choose(null, autoPlacement: false, preferred: PositionChoice.TopLeft));
    }

    [Fact]
    public void FullscreenGameStillWinsOverThePreferredPosition()
    {
        var game = new ForegroundInfo(ForegroundCategory.Game, IsFullscreen: true, IsMaximized: false);

        Assert.Equal(PlacementAnchor.MidLeft, PlacementRules.Choose(game, preferred: PositionChoice.TopRight));
    }

    [Fact]
    public void DefaultPresetKeepsEveryPriority()
    {
        Assert.Equal(70, PriorityRules.PriorityFor(ActivityKind.Media, 70, PriorityPreset.Bawaan));
        Assert.Equal(80, PriorityRules.PriorityFor(ActivityKind.Timer, 80, PriorityPreset.Bawaan));
    }

    [Fact]
    public void PresetOnlyBoostsTheChosenKind()
    {
        Assert.Equal(PriorityRules.BoostedPriority, PriorityRules.PriorityFor(ActivityKind.Media, 70, PriorityPreset.MediaUtama));
        Assert.Equal(80, PriorityRules.PriorityFor(ActivityKind.Timer, 80, PriorityPreset.MediaUtama));

        Assert.Equal(PriorityRules.BoostedPriority, PriorityRules.PriorityFor(ActivityKind.Timer, 80, PriorityPreset.TimerUtama));
        Assert.Equal(70, PriorityRules.PriorityFor(ActivityKind.Media, 70, PriorityPreset.TimerUtama));
    }

    [Fact]
    public void ExplicitHigherPriorityIsNeverLowered()
    {
        Assert.Equal(100, PriorityRules.PriorityFor(ActivityKind.Media, 100, PriorityPreset.MediaUtama));
    }

    [Fact]
    public void PresetChangesThePrimaryActivityWithoutRestartingAnything()
    {
        var controller = new IslandController();

        controller.Handle(new IslandEvent.Upsert(new Activity("lagu", ActivityKind.Media, title: "Lagu A")));
        controller.Handle(new IslandEvent.Upsert(new Activity("timer", ActivityKind.Timer)));

        Assert.Equal("timer", controller.Presentation.Primary!.Id);

        controller.PriorityPreset = PriorityPreset.MediaUtama;
        Assert.Equal("lagu", controller.Presentation.Primary!.Id);

        controller.PriorityPreset = PriorityPreset.Bawaan;
        Assert.Equal("timer", controller.Presentation.Primary!.Id);
    }

    [Fact]
    public void NewActivitiesFollowTheActivePreset()
    {
        var controller = new IslandController { PriorityPreset = PriorityPreset.MediaUtama };

        controller.Handle(new IslandEvent.Upsert(new Activity("lagu", ActivityKind.Media)));
        controller.Handle(new IslandEvent.Upsert(new Activity("timer", ActivityKind.Timer)));

        Assert.Equal("lagu", controller.Presentation.Primary!.Id);
    }

    [Fact]
    public void CustomizationSurvivesASettingsRoundTrip()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "island-custom-" + Guid.NewGuid().ToString("N"),
            "settings.json");

        try
        {
            var saved = new IslandSettings(
                DisplayMode.Ringkas,
                ShowsSecondary: true,
                AutoPlacement: false,
                Language: Language.En,
                Humor: HumorMode.Kocak,
                Accent: AccentChoice.Ungu,
                Position: PositionChoice.TopRight,
                Priority: PriorityPreset.TimerUtama);

            Assert.True(SettingsStore.Save(saved, path));
            Assert.Equal(saved, SettingsStore.Load(path));
        }
        finally
        {
            var folder = System.IO.Path.GetDirectoryName(path)!;
            if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void NewSettingsDefaultToJokeyIndonesian()
    {
        var settings = new IslandSettings();

        Assert.Equal(Language.Id, settings.Language);
        Assert.Equal(HumorMode.Kocak, settings.Humor);
        Assert.Equal(AccentChoice.Oranye, settings.Accent);
        Assert.Equal(PositionChoice.TopCenter, settings.Position);
        Assert.Equal(PriorityPreset.Bawaan, settings.Priority);
        Assert.False(settings.NotificationsEnabled);
    }
}
