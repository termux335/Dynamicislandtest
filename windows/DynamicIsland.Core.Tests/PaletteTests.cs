using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class PaletteTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    private static ClipboardItem Clip(string text, ClipboardKind kind = ClipboardKind.Text, bool redacted = false) =>
        new(kind, text, Now, redacted);

    [Fact]
    public void CalculationComesFirst()
    {
        var entries = Palette.Build("15% dari 240 ribu");

        Assert.Equal(PaletteKind.Calculation, entries[0].Kind);
        Assert.Equal("36.000", entries[0].Title);
        Assert.Equal("36.000", entries[0].Payload);
    }

    [Fact]
    public void ShortcutsAreFilteredByQuery()
    {
        var shortcuts = new[]
        {
            new Shortcut("a", "Visual Studio Code", @"C:\Apps\code.exe"),
            new Shortcut("b", "Spotify", @"C:\Apps\spotify.exe"),
        };

        var entries = Palette.Build("spot", shortcuts);

        var entry = Assert.Single(entries);
        Assert.Equal("Spotify", entry.Title);
        Assert.Equal(PaletteKind.Shortcut, entry.Kind);
    }

    [Fact]
    public void ClipboardMatchesAreSearchable()
    {
        var entries = Palette.Build("rapat", clipboard: new[] { Clip("Catatan rapat"), Clip("Belanja") });

        var entry = Assert.Single(entries);
        Assert.Equal("Catatan rapat", entry.Title);
        Assert.Equal(PaletteKind.Clipboard, entry.Kind);
    }

    [Fact]
    public void EmptyQueryListsShortcutsWithoutClipboard()
    {
        var shortcuts = new[] { new Shortcut("a", "Editor", @"C:\Apps\editor.exe") };

        var entries = Palette.Build(string.Empty, shortcuts, new[] { Clip("rahasia") });

        Assert.Single(entries);
        Assert.Equal("Editor", entries[0].Title);
    }

    [Fact]
    public void SudahDisamarkanTidakBocorLewatPalette()
    {
        var history = new ClipboardHistory(now: () => Now);
        history.Add(ClipboardKind.Text, "password: hunter2");

        var entries = Palette.Build("••", clipboard: history.Items);

        Assert.Equal(ClipboardSafety.Mask, entries[0].Title);
        Assert.DoesNotContain(history.Items, item => item.Text.Contains("hunter2", StringComparison.Ordinal));
    }

    [Fact]
    public void ShortcutStoreRoundTripsAndReplacesByPath()
    {
        var folder = Path.Combine(Path.GetTempPath(), "island-shortcuts-" + Guid.NewGuid().ToString("N"));
        var file = Path.Combine(folder, "shortcuts.json");

        try
        {
            var store = new ShortcutStore(file);
            Assert.True(store.Remember(new Shortcut("", "Editor", @"C:\Apps\editor.exe")));
            Assert.True(store.Remember(new Shortcut("", "Editor baru", @"C:\Apps\editor.exe")));
            Assert.True(store.Save());

            var loaded = new ShortcutStore(file);
            Assert.True(loaded.Load());

            var shortcut = Assert.Single(loaded.All);
            Assert.Equal("Editor baru", shortcut.Label);
            Assert.False(string.IsNullOrEmpty(shortcut.Id));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }
}
