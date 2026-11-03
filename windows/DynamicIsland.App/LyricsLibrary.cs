using System.IO;
using DynamicIsland.Core;

namespace DynamicIsland.App;

/// <summary>
/// Lokasi folder lirik untuk app ini. Pencarian dan pembacaan berkas ada di
/// <see cref="Core.LyricsLibrary"/> supaya bisa dites tanpa UI.
///
/// Folder bawaan <c>%APPDATA%\DynamicIsland\lyrics</c>, bisa dialihkan dengan
/// <c>ISLAND_LYRICS</c>.
/// </summary>
internal static class LyricsSource
{
    public static string Folder => Environment.GetEnvironmentVariable("ISLAND_LYRICS") is { Length: > 0 } custom
        ? custom
        : Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DynamicIsland",
            "lyrics");

    /// <summary>Lirik untuk satu lagu, sekaligus dicatat ke log bila ditemukan.</summary>
    public static IReadOnlyList<LyricLine> Load(string? artist, string title)
    {
        var lines = Core.LyricsLibrary.Load(Folder, artist, title);
        if (lines.Count > 0) Log.Write($"lirik: {lines.Count} baris untuk {title}");

        return lines;
    }
}
