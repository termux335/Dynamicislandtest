using System.Text.RegularExpressions;

namespace DynamicIsland.Core;

/// <summary>Hasil satu ucapan: teks catatan dan/atau waktu pengingat.</summary>
public sealed record VoiceNoteResult(string Text, TimeSpan? ReminderAfter = null, DateTimeOffset? RemindAt = null)
{
    public bool HasReminder => RemindAfter || RemindAt is not null;

    private bool RemindAfter => ReminderAfter is { } after && after > TimeSpan.Zero;
}

/// <summary>
/// Mengubah transkrip ucapan menjadi catatan atau pengingat (Tahap 5b).
///
/// Contoh: "ingatkan 10 menit minum air" → pengingat 10 menit, teks "minum air";
/// "ingatkan pukul 14:30 rapat" → pengingat hari ini/berikutnya;
/// "catat beli susu" → catatan biasa.
///
/// Pengenalan suara ada di lapisan App dan memakai mesin di perangkat; berkas ini
/// hanya pengurai teks supaya bisa dites.
/// </summary>
public static class VoiceNotes
{
    private static readonly Regex Relative = new(
        @"^\s*(?:tolong\s+)?(?:ingatkan|ingatkan saya|remind me|remind)\s+(?<amount>\d+)\s*(?<unit>menit|jam|minute|minutes|hour|hours)\s+(?:untuk\s+|buat\s+|to\s+)?(?<text>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex Clock = new(
        @"^\s*(?:tolong\s+)?(?:ingatkan|ingatkan saya|remind me|remind)\s+(?:saya\s+)?(?:pukul|jam|at)\s*(?<hour>\d{1,2})(?:[:.](?<minute>\d{2}))?\s+(?<text>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex Prefix = new(
        @"^\s*(?:catat|note|buat catatan|tambahkan catatan)\s*[:\-]?\s*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static VoiceNoteResult Parse(string? transcript, DateTimeOffset now)
    {
        var text = transcript?.Trim() ?? string.Empty;
        if (text.Length == 0) return new VoiceNoteResult(string.Empty);

        var relative = Relative.Match(text);
        if (relative.Success)
        {
            var amount = int.Parse(relative.Groups["amount"].Value, System.Globalization.CultureInfo.InvariantCulture);
            var unit = relative.Groups["unit"].Value.ToLowerInvariant();
            var after = unit is "jam" or "hour" or "hours" ? TimeSpan.FromHours(amount) : TimeSpan.FromMinutes(amount);

            return new VoiceNoteResult(relative.Groups["text"].Value.Trim(), after, now + after);
        }

        var clock = Clock.Match(text);
        if (clock.Success)
        {
            var hour = int.Parse(clock.Groups["hour"].Value, System.Globalization.CultureInfo.InvariantCulture);
            var minute = clock.Groups["minute"].Success
                ? int.Parse(clock.Groups["minute"].Value, System.Globalization.CultureInfo.InvariantCulture)
                : 0;

            if (hour is >= 0 and < 24 && minute is >= 0 and < 60)
            {
                var at = new DateTimeOffset(now.Year, now.Month, now.Day, hour, minute, 0, now.Offset);
                if (at <= now) at = at.AddDays(1);

                return new VoiceNoteResult(clock.Groups["text"].Value.Trim(), at - now, at);
            }
        }

        return new VoiceNoteResult(Prefix.Replace(text, string.Empty).Trim());
    }
}
