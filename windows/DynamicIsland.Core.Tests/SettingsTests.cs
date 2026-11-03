using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class SettingsTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "island-settings-" + Guid.NewGuid().ToString("N"));

    private string Path_ => Path.Combine(folder, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void MissingFileReturnsDefaults()
    {
        var settings = SettingsStore.Load(Path_);

        Assert.Equal(new IslandSettings(), settings);
        Assert.Equal(DisplayMode.Standar, settings.DisplayMode);
        Assert.False(settings.ShowsSecondary);
        Assert.True(settings.AutoPlacement);
    }

    [Fact]
    public void ValuesSurviveARoundTrip()
    {
        var saved = new IslandSettings(DisplayMode.Ringkas, ShowsSecondary: true, AutoPlacement: false);

        Assert.True(SettingsStore.Save(saved, Path_));
        Assert.Equal(saved, SettingsStore.Load(Path_));
    }

    [Fact]
    public void FileIsReadableJsonWithEnumNames()
    {
        SettingsStore.Save(new IslandSettings(DisplayMode.Ringkas), Path_);

        var json = File.ReadAllText(Path_);

        Assert.Contains("\"Ringkas\"", json);
        Assert.Contains("\"ShowsSecondary\"", json);
    }

    [Fact]
    public void BrokenJsonFallsBackToDefaults()
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path_, "{ bukan json");

        Assert.Equal(new IslandSettings(), SettingsStore.Load(Path_));
    }

    [Fact]
    public void Stage5BSwitchesSurviveARoundTripWithoutTouchingOtherModules()
    {
        var saved = new IslandSettings(Stage5B: new Stage5BSettings(
            NotesEnabled: false,
            QuietHoursEnabled: true,
            LargeText: true,
            FocusMusic: "https://music.example.com/fokus"));

        Assert.True(SettingsStore.Save(saved, Path_));

        var loaded = SettingsStore.Load(Path_);

        Assert.NotNull(loaded.Stage5B);
        Assert.False(loaded.Stage5B!.NotesEnabled);
        Assert.True(loaded.Stage5B.QuietHoursEnabled);
        Assert.True(loaded.Stage5B.LargeText);
        Assert.Equal("https://music.example.com/fokus", loaded.Stage5B.FocusMusic);
        Assert.True(loaded.SafeShare);
    }

    [Fact]
    public void Stage5CSwitchesSurviveARoundTripAndKeepInternetDefaultsOff()
    {
        var saved = new IslandSettings(Stage5C: new Stage5CSettings(
            AgentStatusEnabled: true,
            TickerEnabled: true,
            TickerUrl: "https://example.com/rss",
            ScreenshotEnabled: false));

        Assert.True(SettingsStore.Save(saved, Path_));

        var loaded = SettingsStore.Load(Path_);

        Assert.NotNull(loaded.Stage5C);
        Assert.True(loaded.Stage5C!.AgentStatusEnabled);
        Assert.True(loaded.Stage5C.TickerEnabled);
        Assert.Equal("https://example.com/rss", loaded.Stage5C.TickerUrl);
        Assert.False(loaded.Stage5C.ScreenshotEnabled);
        Assert.False(loaded.Stage5C.AssistantEnabled);
        Assert.True(loaded.SafeShare);
    }

    [Fact]
    public void SaveCreatesTheFolder()
    {
        Assert.True(SettingsStore.Save(new IslandSettings(), Path_));
        Assert.True(File.Exists(Path_));
    }
}
