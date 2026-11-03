using System.Globalization;
using System.Text.RegularExpressions;

namespace DynamicIsland.Core;

/// <summary>Satu baris lirik pada waktu tertentu.</summary>
public sealed record LyricLine(TimeSpan At, string Text);

/// <summary>
/// Parser lirik berformat LRC (`.lrc`) dan pemilih baris untuk posisi lagu.
///
/// Semua lokal: berkas dibaca dari cakram oleh lapisan UI, dan tidak ada lirik
/// yang diunduh. Format yang didukung: tag `[mm:ss]`, `[mm:ss.xx]`, `[mm:ss.xxx]`,
/// beberapa tag pada satu baris, serta tag metadata (`[ar:...]`) yang dilewati.
/// </summary>
public static partial class Lyrics
{
    /// <summary>Membaca teks LRC menjadi baris berurut waktu. Kosong bila tidak ada lirik.</summary>
    public static IReadOnlyList<LyricLine> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<LyricLine>();

        var lines = new List<LyricLine>();

        foreach (var raw in text.Split('\n'))
        {
            var stamps = TimeTag().Matches(raw);
            if (stamps.Count == 0) continue;

            var content = TimeTag().Replace(raw, string.Empty).Trim();
            if (content.Length == 0) continue;

            foreach (Match stamp in stamps)
            {
                lines.Add(new LyricLine(Timestamp(stamp), content));
            }
        }

        lines.Sort((left, right) => left.At.CompareTo(right.At));
        return lines;
    }

    /// <summary>
    /// Baris yang sedang berlaku pada <paramref name="position"/>: baris terakhir
    /// yang waktunya sudah lewat. Null bila belum ada baris yang lewat.
    /// </summary>
    public static string? At(IReadOnlyList<LyricLine> lines, TimeSpan position)
    {
        if (lines.Count == 0) return null;

        string? current = null;

        foreach (var line in lines)
        {
            if (line.At > position) break;
            current = line.Text;
        }

        return current;
    }

    private static TimeSpan Timestamp(Match match)
    {
        var minutes = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var seconds = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var milliseconds = Milliseconds(match.Groups[3].Value);

        return new TimeSpan(0, 0, minutes, seconds, milliseconds);
    }

    /// <summary>`[00:12.3]` = 300 ms, `[00:12.34]` = 340 ms, `[00:12.345]` = 345 ms.</summary>
    private static int Milliseconds(string? fraction) => fraction switch
    {
        null or "" => 0,
        { Length: 1 } value => int.Parse(value, CultureInfo.InvariantCulture) * 100,
        { Length: 2 } value => int.Parse(value, CultureInfo.InvariantCulture) * 10,
        { Length: > 3 } value => int.Parse(value[..3], CultureInfo.InvariantCulture),
        _ => int.Parse(fraction, CultureInfo.InvariantCulture),
    };

    [GeneratedRegex(@"\[(\d{1,3}):(\d{2})(?:[.:](\d{1,3}))?\]")]
    private static partial Regex TimeTag();
}

/// <summary>
/// Nama berkas lirik yang dicoba untuk satu lagu, tanpa menyentuh cakram:
/// <c>Artis - Judul.lrc</c> lalu <c>Judul.lrc</c>.
/// </summary>
public static class LyricsFiles
{
    public static IReadOnlyList<string> Candidates(string? artist, string title)
    {
        var safeTitle = Safe(title);
        if (safeTitle.Length == 0) return Array.Empty<string>();

        var safeArtist = Safe(artist);
        return safeArtist.Length > 0
            ? new[] { $"{safeArtist} - {safeTitle}", safeTitle }
            : new[] { safeTitle };
    }

    /// <summary>Membuang karakter yang tidak sah sebagai nama berkas Windows.</summary>
    public static string Safe(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new System.Text.StringBuilder(text.Length);

        foreach (var character in text.Trim())
        {
            builder.Append(Array.IndexOf(invalid, character) >= 0 ? '_' : character);
        }

        return builder.ToString().Trim();
    }
}
