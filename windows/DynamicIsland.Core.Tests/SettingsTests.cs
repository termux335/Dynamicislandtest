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
    public void SaveCreatesTheFolder()
    {
        Assert.True(SettingsStore.Save(new IslandSettings(), Path_));
        Assert.True(File.Exists(Path_));
    }
}
