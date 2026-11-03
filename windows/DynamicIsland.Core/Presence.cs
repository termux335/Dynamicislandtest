namespace DynamicIsland.Core;

/// <summary>
/// Preferensi modul Tahap 5b. Setiap modul punya saklar sendiri supaya bisa
/// dimatikan tanpa memengaruhi modul lain.
/// </summary>
public sealed record Stage5BSettings(
    bool NotesEnabled = true,
    bool TypingDeferral = true,
    bool QuietHoursEnabled = false,
    int QuietStartHour = 22,
    int QuietEndHour = 7,
    string? FocusMusic = null,
    bool SpeakNotifications = false,
    bool HighContrast = false,
    bool LargeText = false,
    bool VoiceNotes = false,
    bool AlbumColor = false,
    bool Visualizer = true);

/// <summary>
/// Tunda notifikasi saat mengetik (Tahap 5b).
///
/// Hanya memakai selisih waktu input terakhir (<c>GetLastInputInfo</c>). Tidak
/// ada hook keyboard dan tidak ada isi ketikan yang dibaca atau disimpan.
/// </summary>
public static class TypingRules
{
    /// <summary>Idle di bawah ini berarti pengguna sedang aktif mengetik.</summary>
    public static TimeSpan ActiveWindow { get; } = TimeSpan.FromMilliseconds(1200);

    /// <summary>Idle selama ini berarti pengguna berhenti; notifikasi ditahan tadi dilepas.</summary>
    public static TimeSpan ReleaseDelay { get; } = TimeSpan.FromMilliseconds(1500);

    public static bool ShouldHold(TimeSpan idle, bool enabled = true) =>
        enabled && idle <= ActiveWindow;

    public static bool ShouldRelease(TimeSpan idle, bool enabled = true) =>
        !enabled || idle >= ReleaseDelay;
}

/// <summary>Jam tenang otomatis (Tahap 5b): redup, hanya peringatan kritis.</summary>
public static class QuietHoursRules
{
    public const double Opacity = 0.65;

    public static bool IsQuiet(DateTimeOffset at, bool enabled, int startHour, int endHour)
    {
        if (!enabled) return false;

        var start = Math.Clamp(startHour, 0, 23);
        var end = Math.Clamp(endHour, 0, 23);

        if (start == end) return false;

        var hour = at.Hour;
        return start < end
            ? hour >= start && hour < end
            : hour >= start || hour < end;
    }
}

/// <summary>
/// Musik fokus (Tahap 5b): hanya memutar playlist yang dipilih pengguna.
/// App tidak membawa atau mengunduh musik apa pun.
/// </summary>
public static class FocusMusicRules
{
    public static bool CanPlay(string? target)
    {
        if (string.IsNullOrWhiteSpace(target)) return false;

        var value = target.Trim();

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return true;
        }

        return File.Exists(value);
    }

    public static bool ShouldStart(bool enabled, string? target) =>
        enabled && CanPlay(target);
}
