namespace DynamicIsland.Core;

/// <summary>
/// Preferensi perilaku kocak (bisa dimatikan satu per satu sesuai CLAUDE.windows.md).
/// Bila HumorMode adalah Serius, semua perilaku kocak otomatis tidak aktif.
/// </summary>
public sealed record HumorSettings(
    bool Eyes = true,
    bool Tickle = true,
    bool Sulking = true,
    bool RepeatTrack = true,
    bool DramaticCountdown = true,
    bool EasterEgg = true);

/// <summary>
/// Menghitung pergeseran pupil mata kapsul saat idle mengikuti posisi kursor kursor.
/// </summary>
public static class EyeGaze
{
    public const double MaxTravel = 2.5;

    public static (double OffsetX, double OffsetY) Calculate(
        double capsuleCenterX,
        double capsuleCenterY,
        double cursorX,
        double cursorY,
        double maxRadius = MaxTravel)
    {
        var dx = cursorX - capsuleCenterX;
        var dy = cursorY - capsuleCenterY;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));

        if (distance < 0.001) return (0, 0);

        // Semakin dekat kursor ke kapsul, pergeseran pupil proporsional sampai radius 120 px.
        var factor = Math.Clamp(distance / 120.0, 0.2, 1.0);
        var unitX = dx / distance;
        var unitY = dy / distance;

        return (unitX * maxRadius * factor, unitY * maxRadius * factor);
    }
}

/// <summary>
/// Mendeteksi klik kapsul 5 kali cepat dalam jendela 1,2 detik.
/// </summary>
public sealed class TickleDetector
{
    private readonly List<DateTimeOffset> clicks = new();
    private static readonly TimeSpan Window = TimeSpan.FromMilliseconds(1200);

    public int RequiredClicks { get; set; } = 5;

    public bool RegisterClick(DateTimeOffset now)
    {
        clicks.Add(now);
        clicks.RemoveAll(time => now - time > Window);

        if (clicks.Count >= RequiredClicks)
        {
            clicks.Clear();
            return true;
        }

        return false;
    }

    public void Reset() => clicks.Clear();
}

/// <summary>
/// Kapsul ngambek: kalau diabaikan 10 menit saat timer berjalan, teksnya berganti.
/// </summary>
public static class SulkingDetector
{
    public static readonly TimeSpan Threshold = TimeSpan.FromMinutes(10);

    public static bool IsSulking(bool timerRunning, TimeSpan timerElapsed, TimeSpan idleDuration) =>
        timerRunning && timerElapsed >= Threshold && idleDuration >= Threshold;
}

/// <summary>
/// Mendeteksi jika lagu yang sama diputar 3 kali beruntun.
/// </summary>
public sealed class RepeatTrackDetector
{
    public string? CurrentTrack { get; private set; }
    public int RepeatCount { get; private set; }

    public bool TrackPlayed(string title, string? artist)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            CurrentTrack = null;
            RepeatCount = 0;
            return false;
        }

        var trackKey = $"{title.Trim()} - {(artist ?? string.Empty).Trim()}".ToLowerInvariant();

        if (string.Equals(CurrentTrack, trackKey, StringComparison.OrdinalIgnoreCase))
        {
            RepeatCount++;
            return RepeatCount == 3;
        }

        CurrentTrack = trackKey;
        RepeatCount = 1;
        return false;
    }

    public void Reset()
    {
        CurrentTrack = null;
        RepeatCount = 0;
    }
}

/// <summary>
/// Hitung mundur drama: 10 detik terakhir timer, kapsul berdenyut makin cepat.
/// </summary>
public static class DramaTimer
{
    public static readonly TimeSpan DramaThreshold = TimeSpan.FromSeconds(10);

    public static bool IsDramatic(TimeSpan remaining) =>
        remaining > TimeSpan.Zero && remaining <= DramaThreshold;

    public static double PulseFrequency(TimeSpan remaining)
    {
        if (!IsDramatic(remaining)) return 0;

        var seconds = remaining.TotalSeconds;
        // Dari 1 Hz pada 10 detik hingga 4 Hz pada 1 detik.
        return 1.0 + (3.0 * (1.0 - Math.Clamp(seconds / 10.0, 0.0, 1.0)));
    }
}

/// <summary>
/// Telur paskah: urutan tombol tertentu (misalnya kata "island")
/// memicu wujud rahasia kapsul selama 3 detik.
/// </summary>
public sealed class EasterEggSequence
{
    private readonly string targetSequence;
    private readonly List<(char Key, DateTimeOffset Time)> buffer = new();
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(4);

    public EasterEggSequence(string sequence = "island")
    {
        targetSequence = sequence.ToLowerInvariant();
    }

    public bool RegisterKey(char key, DateTimeOffset now)
    {
        buffer.RemoveAll(entry => now - entry.Time > Timeout);
        buffer.Add((char.ToLowerInvariant(key), now));

        if (buffer.Count < targetSequence.Length) return false;

        var tail = new string(buffer.TakeLast(targetSequence.Length).Select(b => b.Key).ToArray());
        if (tail == targetSequence)
        {
            buffer.Clear();
            return true;
        }

        return false;
    }

    public void Reset() => buffer.Clear();
}
