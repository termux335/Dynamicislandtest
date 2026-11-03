using System.IO;
using System.Text;

namespace DynamicIsland.Core;

/// <summary>
/// Mencari dan membaca berkas lirik `.lrc` di satu folder.
///
/// Berkas hanya dibaca dari cakram; tidak ada unduhan atau jaringan. Pencocokan
/// nama memakai <see cref="LyricsFiles"/> dan tanpa membedakan huruf besar.
/// Folder dipilih lapisan UI (mis. <c>%APPDATA%\DynamicIsland\lyrics</c>).
/// </summary>
public static class LyricsLibrary
{
    /// <summary>Baris lirik untuk satu lagu; kosong bila tidak ada berkas yang cocok.</summary>
    public static IReadOnlyList<LyricLine> Load(string folder, string? artist, string title)
    {
        foreach (var candidate in LyricsFiles.Candidates(artist, title))
        {
            var file = Find(folder, candidate);
            if (file is null) continue;

            try
            {
                var lines = Lyrics.Parse(File.ReadAllText(file, Encoding.UTF8));
                if (lines.Count > 0) return lines;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or DecoderFallbackException)
            {
                // Berkas tidak terbaca: coba kandidat berikutnya, jangan menjatuhkan app.
            }
        }

        return Array.Empty<LyricLine>();
    }

    /// <summary>Path berkas lirik untuk satu kandidat nama, atau null.</summary>
    public static string? Find(string folder, string name)
    {
        try
        {
            if (!Directory.Exists(folder)) return null;

            var exact = Path.Combine(folder, name + ".lrc");
            if (File.Exists(exact)) return exact;

            return Directory
                .EnumerateFiles(folder, "*.lrc")
                .FirstOrDefault(file => string.Equals(
                    Path.GetFileNameWithoutExtension(file),
                    name,
                    StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            return null;
        }
    }
}
