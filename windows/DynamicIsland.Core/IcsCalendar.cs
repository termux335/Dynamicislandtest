using System.Globalization;
using System.Text;

namespace DynamicIsland.Core;

/// <summary>
/// Pembaca berkas kalender <c>.ics</c> lokal (Tahap 2).
///
/// Hanya properti yang dibutuhkan kapsul: judul, waktu mulai/selesai, deskripsi,
/// dan lokasi (untuk tautan pertemuan). Tidak membaca jaringan dan tidak butuh
/// izin; berkas dibaca dari path yang diberikan app.
/// </summary>
public static class IcsCalendar
{
    private static readonly TimeSpan DefaultEventLength = TimeSpan.FromMinutes(30);

    private static readonly string[] LocalDateTimeFormats =
    {
        "yyyyMMdd'T'HHmmss",
        "yyyyMMdd'T'HHmm",
    };

    /// <summary>Membaca semua VEVENT pada teks kalender. Baris rusak dilewati.</summary>
    public static IReadOnlyList<CalendarEvent> Parse(string? text)
    {
        var events = new List<CalendarEvent>();
        if (string.IsNullOrWhiteSpace(text)) return events;

        Dictionary<string, string>? current = null;

        foreach (var line in Unfold(text))
        {
            var trimmed = line.Trim();

            if (trimmed.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            if (trimmed.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                if (current is not null && BuildEvent(current) is { } item) events.Add(item);
                current = null;
                continue;
            }

            if (current is null) continue;

            var colon = trimmed.IndexOf(':');
            if (colon <= 0) continue;

            var header = trimmed[..colon];
            var value = trimmed[(colon + 1)..];

            // Nama properti bisa punya parameter, mis. DTSTART;VALUE=DATE:20260101.
            var semicolon = header.IndexOf(';');
            var name = semicolon >= 0 ? header[..semicolon] : header;
            if (name.Length == 0) continue;

            current[name] = value;
        }

        return events;
    }

    private static CalendarEvent? BuildEvent(Dictionary<string, string> properties)
    {
        if (!properties.TryGetValue("DTSTART", out var startText)) return null;
        if (ParseStart(startText) is not { } start) return null;

        var end = properties.TryGetValue("DTEND", out var endText) && ParseStart(endText) is { } parsedEnd
            ? parsedEnd
            : start + DefaultEventLength;

        if (end <= start) end = start + DefaultEventLength;

        var title = properties.TryGetValue("SUMMARY", out var summary) ? Unescape(summary).Trim() : string.Empty;
        if (title.Length == 0) title = "Tanpa judul";

        var description = properties.TryGetValue("DESCRIPTION", out var rawDescription) ? Unescape(rawDescription) : null;
        var location = properties.TryGetValue("LOCATION", out var rawLocation) ? Unescape(rawLocation) : null;

        var id = properties.TryGetValue("UID", out var uid) && uid.Trim().Length > 0
            ? uid.Trim()
            : $"{start:O}-{title}";

        return new CalendarEvent(
            Id: id,
            Title: title,
            StartAt: start,
            EndAt: end,
            MeetingUrl: CalendarPlan.MeetingUrl(description, location));
    }

    /// <summary>Tanggal saja (VALUE=DATE) atau waktu UTC/lokal.</summary>
    private static DateTimeOffset? ParseStart(string rawValue)
    {
        var value = rawValue.Trim();

        if (value.Length == 8 &&
            DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
        {
            return new DateTimeOffset(dateOnly);
        }

        if (value.EndsWith('Z') &&
            DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utc))
        {
            return new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
        }

        return DateTime.TryParseExact(value, LocalDateTimeFormats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var local)
            ? new DateTimeOffset(local)
            : null;
    }

    /// <summary>Menggabungkan baris lanjutan (RFC 5545: baris berikutnya diawali spasi/tab).</summary>
    private static IEnumerable<string> Unfold(string text)
    {
        var builder = new StringBuilder();

        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0 && (line[0] == ' ' || line[0] == '\t'))
            {
                builder.Append(line[1..]);
                continue;
            }

            if (builder.Length > 0) yield return builder.ToString();
            builder.Clear().Append(line);
        }

        if (builder.Length > 0) yield return builder.ToString();
    }

    /// <summary>Teks ICS: <c>\\n</c> jadi baris baru, koma/titik koma/backslash dilepas escape-nya.</summary>
    private static string Unescape(string value)
    {
        var builder = new StringBuilder(value.Length);

        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\' || i == value.Length - 1)
            {
                builder.Append(value[i]);
                continue;
            }

            i++;
            builder.Append(value[i] switch
            {
                'n' or 'N' => '\n',
                _ => value[i],
            });
        }

        return builder.ToString();
    }
}
