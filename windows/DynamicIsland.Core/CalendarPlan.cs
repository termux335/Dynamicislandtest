using System.Text.RegularExpressions;

namespace DynamicIsland.Core;

/// <summary>
/// Acara kalender berikutnya (Tahap 2): hitung mundur dan tombol gabung.
///
/// Pembacaan berkas <c>.ics</c> ada di <see cref="IcsCalendar"/>; bagian ini
/// murni logika supaya bisa dites tanpa sistem.
/// </summary>
public sealed record CalendarEvent(
    string Id,
    string Title,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string? MeetingUrl = null);

public static partial class CalendarPlan
{
    /// <summary>Id kapsul: hanya satu acara berikutnya yang tampil.</summary>
    public const string Id = "calendar-next";

    /// <summary>Jendela pencarian acara berikutnya.</summary>
    public static readonly TimeSpan Lookahead = TimeSpan.FromHours(24);

    /// <summary>Acara paling awal yang belum selesai pada <paramref name="now"/>.</summary>
    public static CalendarEvent? Next(IEnumerable<CalendarEvent> events, DateTimeOffset now) =>
        events.Where(item => item.EndAt > now)
            .OrderBy(item => item.StartAt)
            .FirstOrDefault();

    /// <summary>Teks hitung mundur; gaya bahasanya mengikuti <paramref name="text"/>.</summary>
    public static string CountdownLabel(CalendarEvent item, DateTimeOffset now, IslandText? text = null)
    {
        var strings = text ?? IslandText.Default;

        return now < item.StartAt
            ? strings.Format("calendar.startsIn", Duration(item.StartAt - now, text))
            : strings.Format("calendar.endsIn", Duration(item.EndAt - now < TimeSpan.Zero ? TimeSpan.Zero : item.EndAt - now, text));
    }

    /// <summary>Durasi pendek; menit ke bawah, detik hanya bila kurang dari satu menit.</summary>
    public static string Duration(TimeSpan span, IslandText? text = null)
    {
        var strings = text ?? IslandText.Default;
        var totalHours = (long)span.TotalHours;
        var totalMinutes = (long)span.TotalMinutes;

        return totalMinutes switch
        {
            >= 60 when totalMinutes % 60 == 0 => strings.Format("duration.hours", totalHours),
            >= 60 => strings.Format("duration.hoursMinutes", totalHours, totalMinutes % 60),
            > 0 => strings.Format("duration.minutes", totalMinutes),
            _ => strings.Format("duration.seconds", (long)span.TotalSeconds < 1 ? 1 : (long)span.TotalSeconds),
        };
    }

    /// <summary>Kapsul untuk satu acara. Progres tidak dipakai: waktunya ada di subjudul.</summary>
    public static Activity ActivityFor(CalendarEvent item, DateTimeOffset now, IslandText? text = null)
    {
        var strings = text ?? IslandText.Default;

        return new Activity(
            id: Id,
            kind: ActivityKind.Calendar,
            title: item.Title,
            subtitle: CountdownLabel(item, now, strings),
            isLive: true,
            actions: item.MeetingUrl is null
                ? Array.Empty<ActivityAction>()
                : new[] { new ActivityAction("join", strings.Get("calendar.join"), "join") });
    }

    /// <summary>Tautan pertemuan pertama dari deskripsi, lalu dari lokasi.</summary>
    public static string? MeetingUrl(string? description, string? location = null) =>
        FindUrl(description) ?? FindUrl(location);

    private static string? FindUrl(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        var match = UrlPattern().Match(text);
        return match.Success ? match.Value.TrimEnd(',', '.', ';', ')', ']') : null;
    }

    [GeneratedRegex("""https?://[^\s<>"']+""")]
    private static partial Regex UrlPattern();
}
